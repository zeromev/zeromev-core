using System;
using System.Diagnostics;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroMev.MevEFC;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using C5;

namespace ZeroMev.ClassifierService
{
    // a circular buffer of blocks within range of updates when calculating mev
    public static class BlockBuffer
    {
        static MEVBlock2[] _buffer = new MEVBlock2[Config.Settings.BlockBufferSize.Value];

        // TODO code circular buffer
    }

    // load mev-inspect data for a chosen block range to be passed to the zm classifier
    public class BlockInput
    {
        public IEnumerable<ZmBlock> ZmBlocks { get; set; } // TODO consider different input types, batch/startup + realtime

        public IEnumerable<Swap> Swaps { get; set; }
        public IEnumerable<Arbitrage> Arbitrages { get; set; }
        public IEnumerable<Sandwich> Sandwiches { get; set; }
        public IEnumerable<SandwichedSwap> SandwichedSwaps { get; set; }
        public IEnumerable<Liquidation> Liquidations { get; set; }
        public IEnumerable<NftTrade> NftTrades { get; set; }
        public IEnumerable<PunkSnipe> PunkSnipes { get; set; }
        private long _blockNumber = 0;
        private List<DateTime>? _arrivals = null;

        public static BlockInput Build(long fromBlockNumber, long toBlockNumber)
        {
            BlockInput bi = new BlockInput();

            using (var db = new zeromevContext())
            {
                bi.Swaps = (from s in db.Swaps
                            where s.BlockNumber >= fromBlockNumber && s.BlockNumber <= toBlockNumber
                            orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                            select s).ToList();

                bi.Sandwiches = (from sw in db.Sandwiches
                                 where sw.BlockNumber >= fromBlockNumber && sw.BlockNumber <= toBlockNumber
                                 select sw).ToList();

                bi.SandwichedSwaps = (from ss in db.SandwichedSwaps
                                      where ss.BlockNumber >= fromBlockNumber && ss.BlockNumber <= toBlockNumber
                                      select ss).ToList();

                bi.Arbitrages = (from a in db.Arbitrages
                                 where a.BlockNumber >= fromBlockNumber && a.BlockNumber <= toBlockNumber
                                 select a).ToList();

                bi.ZmBlocks = (from zb in db.ZmBlocks
                               where zb.BlockNumber >= fromBlockNumber && zb.BlockNumber <= toBlockNumber
                               orderby zb.BlockNumber
                               select zb).ToList();
            }

            return bi;
        }

        public void Process()
        {
            Tokens.Load();

            Sandwich? sandwich = null;
            ZMSwap? frontrun = null;
            string lastArbHash = null;

            DEXs dexs = new DEXs();
            ZMDecimal? sandwichTotalUSD = 0;
            ZMDecimal? arbTotalUSD = 0;

            foreach (Swap s in Swaps)
            {
                // add swap
                List<DateTime>? arrivals = GetArrivalTimes(s.BlockNumber);
                if (arrivals == null) continue;
                if (s.TransactionPosition == null || s.Error != null) continue;
                var zmSwap = dexs.Add(s, arrivals[s.TransactionPosition.Value], out var pair);

                // sandwiches
                if (sandwich == null)
                {
                    sandwich = this.Sandwiches.FirstOrDefault<Sandwich>(x => { return x.FrontrunSwapTransactionHash == s.TransactionHash && Enumerable.SequenceEqual(x.FrontrunSwapTraceAddress, s.TraceAddress); });
                    if (sandwich != null)
                    {
                        // sandwich frontrun
                        frontrun = zmSwap;
                    }
                    else
                    {
                        // arbitrages
                        var arb = this.Arbitrages.FirstOrDefault<Arbitrage>(x => { return x.TransactionHash == s.TransactionHash; });
                        if (arb != null && arb.TransactionHash != lastArbHash)
                        {
                            ZMDecimal? xrate = XRates.GetUSDRate(arb.ProfitTokenAddress);
                            Tokens.GetSymbol(arb.ProfitTokenAddress, out var divisor);

                            if (xrate != null)
                            {
                                ZMDecimal? victimLoss = -((arb.ProfitAmount / divisor) * xrate);
                                Debug.WriteLine($"arbitrage {victimLoss} usd");
                                arbTotalUSD += victimLoss;
                            }
                            lastArbHash = arb.TransactionHash;
                        }
                    }
                }
                else if (s.TransactionHash == sandwich.BackrunSwapTransactionHash && Enumerable.SequenceEqual(s.TraceAddress, sandwich.BackrunSwapTraceAddress))
                {
                    // sandwich backrun

                    // in/out tokens should be the same, but use distinct rates to be sure
                    ZMDecimal? victimLoss = (frontrun.InAmount * frontrun.InRateUSD) - (zmSwap.OutAmount * zmSwap.OutRateUSD);
                    Debug.WriteLine($"sandwich {victimLoss} usd");
                    sandwichTotalUSD += victimLoss;

                    // look for the next
                    sandwich = null;
                    frontrun = null;
                }

                // victim impact - not yet developed
                /*
                ZMSwap zmSwapPrevByTime;
                if (zmSwap != null && pair != null && zmSwap.ImpactDelta != null && pair.PreviousSwap != null && zmSwap.BRateUSD != null && pair.PreviousSwap.ImpactDelta != null)
                {
                    if (pair.TimeOrder.TryPredecessor(zmSwap.Order, out var prevEntry))
                    {
                        // if the prev swap is of the same sign, it acts against the current swap
                        // if the prev swap is of a different sign, it benefits the current swap
                        // victim impact is expressed as positive when good for the current swap, and negative when bad (frontrun)
                        zmSwapPrevByTime = prevEntry.Value;
                        if (zmSwapPrevByTime.ImpactDelta != null)
                        {
                            ZMDecimal? victimImpact = -(pair.PreviousSwap.ImpactDelta - zmSwapPrevByTime.ImpactDelta);
                            ZMDecimal? victimImpactUSD = victimImpact * zmSwap.BRateUSD;
                            if (zmSwap.ImpactDelta < 0) victimImpact = -victimImpact; // switch sells to give a consistent victim impact
                            if (pair.ToString() == "WETH:USDC")
                                Debug.WriteLine($"{pair} {zmSwap.Order} victim impact ${victimImpactUSD.Value.RoundAwayFromZero(4)} {victimImpact} prev block {pair.PreviousSwap.ImpactDelta} prev time {zmSwapPrevByTime.ImpactDelta} current {zmSwap.ImpactDelta} {zmSwap.IsSell}");
                        }
                    }
                }
                */
                _blockNumber = s.BlockNumber;
            }
            Debug.WriteLine($"{sandwichTotalUSD} sandwich victim loss");
            Debug.WriteLine($"{arbTotalUSD} arb victim loss");
        }

        public List<DateTime>? GetArrivalTimes(long blockNumber)
        {
            if (blockNumber != _blockNumber)
            {
                List<DateTime>? arrivals = null;
                ZmBlock zb = ZmBlocks.First<ZmBlock>(x => x.BlockNumber == blockNumber);
                if (zb != null)
                {
                    var txData = Binary.Decompress(zb.TxData);
                    arrivals = Binary.ReadFirstSeenTxData(txData);
                    Debug.Assert(arrivals.Count == zb.TransactionCount);
                }
                _blockNumber = blockNumber;
                _arrivals = arrivals;
            }
            return _arrivals;
        }
    }

    public class XRate
    {
        public ZMDecimal Rate;

        public XRate()
        {
        }

        public XRate(ZMDecimal rate)
        {
            Rate = rate;
        }
    }

    // not threadsafe
    public static class XRates
    {
        public static ZMDecimal? ETHBaseRate { get; set; }
        private static Dictionary<string, XRate> _usdBaseRate = new Dictionary<string, XRate>();
        public static Dictionary<string, BaseCurrency> _baseTokens;

        static XRates()
        {
            _baseTokens = new Dictionary<string, BaseCurrency>
                {
                    // USD
                    {"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48" , BaseCurrency.USD },  // USDC
                    {"0x6b175474e89094c44da98b954eedeac495271d0f" , BaseCurrency.USD },  // DAI
                    {"0xdac17f958d2ee523a2206206994597c13d831ec7" , BaseCurrency.USD },  // TUSD
                    {"0x4fabb145d64652a948d72533023f6e7a623c7c53" , BaseCurrency.USD },  // BUSD
                    {"0x0000000000085d4780B73119b644AE5ecd22b376" , BaseCurrency.USD },  // TrueUSD
                    {"0x056fd409e1d7a124bd7017459dfea2f387b6d5cd" , BaseCurrency.USD },  // GUSD

                    // ETH
                    {"0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2" , BaseCurrency.ETH }   // wETH
                };

            foreach (var t in _baseTokens)
            {
                if (t.Value == BaseCurrency.USD)
                    _usdBaseRate.Add(t.Key, new XRate(1));
            }
        }

        public static bool GetCurrency(string token, out BaseCurrency? currency)
        {
            BaseCurrency c;
            if (!_baseTokens.TryGetValue(token, out c))
            {
                currency = null;
                return false;
            }
            currency = c;
            return true;
        }

        public static ZMDecimal? GetUSDRate(string token)
        {
            _usdBaseRate.TryGetValue(token, out var rate);
            if (rate == null)
                return null;
            return rate.Rate;
        }

        public static void SetUSDBaseRate(string token, ZMDecimal newRate)
        {
            XRate rate;
            if (!_usdBaseRate.TryGetValue(token, out rate))
            {
                rate = new XRate();
                _usdBaseRate.Add(token, rate);
            }
            rate.Rate = newRate;
        }
    }

    // not threadsafe
    public static class Tokens
    {
        public const string Unknown = "???";
        public static readonly BigInteger Pow18 = new BigInteger(1000000000000000000);
        private static Dictionary<string, ZmToken> _tokens = null;

        public static void Load()
        {
            if (_tokens != null)
                return;

            Dictionary<string, ZmToken> newTokens = new Dictionary<string, ZmToken>();
            using (var db = new zeromevContext())
            {
                var tokens = (from t in db.ZmTokens
                              where (t.Symbol != null && t.Symbol != Unknown)
                              select t).ToList();

                foreach (var t in tokens)
                    newTokens.Add(t.Address, t);
            }
            _tokens = newTokens;
        }

        public static void Add(List<ZmToken> addTokens)
        {
            foreach (var t in addTokens)
                _tokens.TryAdd(t.Address, t);
        }

        public static bool Add(ZmToken addToken)
        {
            return _tokens.TryAdd(addToken.Address, addToken);
        }

        public static ZmToken? GetFromAddress(string tokenAddress)
        {
            if (tokenAddress == null) return null;
            if (_tokens.TryGetValue(tokenAddress, out ZmToken? token))
                return token;
            return null;
        }

        public static string GetPairName(string tokenA, string tokenB)
        {
            string a = GetSymbol(tokenA);
            string b = GetSymbol(tokenB);
            return a + ":" + b;
        }

        public static string GetSymbol(string token)
        {
            var t = Tokens.GetFromAddress(token);
            if (t == null || t.Symbol == null || t.Symbol == Unknown) return token;
            return t.Symbol;
        }

        public static string GetSymbol(string token, out ZMDecimal divisor)
        {
            divisor = Pow18; // default to 18 decimals
            var t = Tokens.GetFromAddress(token);
            if (t == null || t.Symbol == null || t.Symbol == Unknown) return token;
            if (t.Decimals.HasValue)
                divisor = t.Divisor;
            return t.Symbol;
        }
    }

    // a root object of a data structure for handling swaps
    public class DEXs : Dictionary<string, DEX>
    {
        public ZMSwap? Add(Swap swap, DateTime arrivalTime, out Pair? pair)
        {
            pair = null;
            if (swap == null) return null;

            // build dex key
            string? key = DEX.BuildKey(swap);
            if (key == null) return null;

            // add new or get existing
            DEX dex;
            if (!this.TryGetValue(key, out dex))
            {
                dex = new DEX(swap.AbiName, swap.Protocol);
                this.Add(key, dex);
            }

            // add the swap to the dex
            return dex.Add(swap, arrivalTime, out pair);
        }
    }

    public class DEX : Dictionary<string, Pair>
    {
        public string? AbiName { get; private set; }
        public string? Protocol { get; private set; }

        public DEX(string? abiName, string? protocol)
        {
            AbiName = abiName;
            Protocol = protocol;
        }

        public ZMSwap Add(Swap swap, DateTime arrivalTime, out Pair pair)
        {
            XRates.GetCurrency(swap.TokenInAddress, out var currencyIn);
            XRates.GetCurrency(swap.TokenOutAddress, out var currencyOut);

            bool isSell;
            pair = GetOrAddPair(swap.TokenInAddress, swap.TokenOutAddress, currencyIn, currencyOut, out isSell);
            BlockOrder blockOrder = new BlockOrder((long)swap.BlockNumber, (int)swap.TransactionPosition.Value, swap.TraceAddress); // if transaction position is ever null, we want it to raise an exception
            return pair.Add(swap, blockOrder, arrivalTime, isSell);
        }

        private Pair GetOrAddPair(string tokenIn, string tokenOut, BaseCurrency? currencyIn, BaseCurrency? currencyOut, out bool IsSell)
        {
            // sort in/out tokens to get a single pair containing both buys and sells
            string tokenA, tokenB;
            BaseCurrency? currencyA, currencyB;
            Pair.ConformPair(tokenIn, tokenOut, currencyIn, currencyOut, out tokenA, out tokenB, out currencyA, out currencyB, out IsSell);

            // create and add if required, then return
            Pair? pair;
            string key = tokenA + tokenB;
            if (!this.TryGetValue(key, out pair))
            {
                bool doSetXRates = false;
                if (AbiName == "UniswapV2Pair" || AbiName == "UniswapV3Pool") // only use uniswap for exchange rates for consistency
                    doSetXRates = true;
                pair = new Pair(this, key, tokenA, tokenB, currencyA, currencyB, doSetXRates);
                this.Add(key, pair);
            }
            return pair;
        }

        public static string? BuildKey(Swap swap)
        {
            if (swap == null)
                return null;

            return (swap.AbiName ?? "") + (swap.Protocol ?? "");
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public enum BaseCurrency
    {
        USD,
        ETH
    }

    public class Pair
    {
        public string Key { get; private set; }
        public DEX Parent { get; private set; }
        public string TokenA { get; private set; }
        public string TokenB { get; private set; }
        public BaseCurrency? BaseCurrencyA { get; private set; }
        public BaseCurrency? BaseCurrencyB { get; private set; }
        public bool DoSetXRates { get; private set; }

        // we always progress by block time, so don't need a list
        public ZMSwap? CurrentSwap = null;
        public ZMSwap? PreviousSwap = null;

        // we need to hop around in arrival time order, so need a sorted collection
        public readonly TreeDictionary<Order, ZMSwap> TimeOrder = new TreeDictionary<Order, ZMSwap>();

        public Pair(DEX parent, string key, string tokenA, string tokenB, BaseCurrency? currencyA, BaseCurrency? currencyB, bool doSetXRates)
        {
            Parent = parent;
            Key = key;
            TokenA = tokenA;
            TokenB = tokenB;
            BaseCurrencyA = currencyA;
            BaseCurrencyB = currencyB;
            DoSetXRates = doSetXRates;
        }

        // add must be called in block order without gaps for accurate mev classification 
        public ZMSwap Add(Swap swap, BlockOrder blockOrder, DateTime arrivalTime, bool isSell)
        {
            // get symbol and decimals for each token
            ZMDecimal inDivisor, outDivisor;
            string symbolIn = Tokens.GetSymbol(swap.TokenInAddress, out inDivisor);
            string symbolOut = Tokens.GetSymbol(swap.TokenOutAddress, out outDivisor);

            // apply the decimal divisor
            ZMDecimal tokenIn = (ZMDecimal)(swap.TokenInAmount / inDivisor);
            ZMDecimal tokenOut = (ZMDecimal)(swap.TokenOutAmount / outDivisor);

            // create a smaller footprint zeromev swap which includes our timing data
            ZMSwap zmSwap = new ZMSwap(blockOrder, arrivalTime, isSell, tokenIn, tokenOut, symbolIn, symbolOut);

            /* TODO victim impact - not yet developed
            // update block order (represented as current and previous swaps)
            PreviousSwap = CurrentSwap;
            CurrentSwap = zmSwap;
            if (PreviousSwap != null)
                CurrentSwap.ImpactDelta = CurrentSwap.ExchangeRate() - PreviousSwap.ExchangeRate();

            // update time order (a sorted collection)
            TimeOrder.Add(zmSwap.Order, zmSwap);
            */

            // some dexs are responsible for setting base rates
            if (DoSetXRates)
            {
                if (BaseCurrencyA == BaseCurrency.ETH && BaseCurrencyB == BaseCurrency.USD)
                {
                    XRates.ETHBaseRate = zmSwap.ExchangeRate();
                    XRates.SetUSDBaseRate(TokenA, zmSwap.ExchangeRate());
                }
                else if (BaseCurrencyA == null && BaseCurrencyB == BaseCurrency.USD)
                {
                    XRates.SetUSDBaseRate(TokenA, zmSwap.ExchangeRate());
                }
                else if (BaseCurrencyA == null && BaseCurrencyB == BaseCurrency.ETH)
                {
                    if (XRates.ETHBaseRate.HasValue)
                    {
                        ZMDecimal usd = zmSwap.ExchangeRate() * XRates.ETHBaseRate.Value;
                        XRates.SetUSDBaseRate(TokenA, usd);
                    }
                }
            }

            // set the latest dollar rates for both sides of the swap
            zmSwap.ARateUSD = XRates.GetUSDRate(TokenA);
            zmSwap.BRateUSD = XRates.GetUSDRate(TokenB);

            return zmSwap;
        }

        // sort in/out tokens to get a single pair which can contain both buys and sells
        // known currency base pairs are placed as tokenB in a predefined priority (eg: ETH:LINK -> LINK:ETH, USDC:ETH -> ETH:USDC, USDC:DAI -> DAI:USDC)
        public static void ConformPair(string tokenIn, string tokenOut, BaseCurrency? currencyIn, BaseCurrency? currencyOut, out string tokenA, out string tokenB, out BaseCurrency? currencyA, out BaseCurrency? currencyB, out bool IsSell)
        {
            // decide symbol priority in pair
            IsSell = false;
            if (currencyIn == currencyOut) // WETH:WETH, USDC:USDC, other:other
            {
                IsSell = (string.Compare(tokenIn, tokenOut) > 0); // alphanumeric order
            }
            else if (currencyIn != null && currencyOut == null) // WETH:other, USDC:other
            {
                IsSell = true;
            }
            else if (currencyIn == BaseCurrency.USD && currencyOut == BaseCurrency.ETH) // USDC:WETH
            {
                IsSell = true;
            }

            if (!IsSell)
            {
                tokenA = tokenIn;
                tokenB = tokenOut;
                currencyA = currencyIn;
                currencyB = currencyOut;
                IsSell = false;
            }
            else
            {
                tokenA = tokenOut;
                tokenB = tokenIn;
                currencyA = currencyOut;
                currencyB = currencyIn;
                IsSell = true;
            }
        }

        public override string ToString()
        {
            return Tokens.GetPairName(TokenA, TokenB);
        }
    }
}