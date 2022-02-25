using System;
using System.Diagnostics;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using ZeroMev.MevEFC;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using C5;

namespace ZeroMev.ClassifierService
{
    // lessens the code needed in light client classes such as MEVBlock
    public static class MEVHelper
    {
        public static int GetSymbolIndex(MEVBlock2 mb, string tokenAddress)
        {
            int index = mb.Symbols.FindIndex(x => { return x.TokenAddress == tokenAddress; });
            if (index == -1)
            {
                var zmToken = Tokens.GetFromAddress(tokenAddress);
                if (zmToken == null)
                    return -1; // meaning unknown symbol

                string image;
                if (zmToken.Image == null)
                    image = Symbol.UnknownImage;
                else
                {
                    image = zmToken.Image.Replace(@"/images/", "");
                }

                var symbol = new Symbol(zmToken.Name, image, tokenAddress);
                mb.Symbols.Add(symbol);
                return mb.Symbols.Count - 1;
            }
            return index;
        }

        public static MEVSwap BuildMEVSwap(MEVBlock2 mb, Swap swap, ZMSwap zmSwap)
        {
            int symbolIn = GetSymbolIndex(mb, swap.TokenInAddress);
            int symbolOut = GetSymbolIndex(mb, swap.TokenOutAddress);
            var mevSwap = new MEVSwap(symbolIn, symbolOut, zmSwap.InAmount, zmSwap.OutAmount, zmSwap.OutRateUSD);
            return mevSwap;
        }
    }

    // load mev-inspect data for a chosen block range to be passed to the zm classifier
    public class BlockProcess
    {
        public IEnumerable<Swap> Swaps { get; set; }
        public IEnumerable<Arbitrage> Arbitrages { get; set; }
        public IEnumerable<Sandwich> Sandwiches { get; set; }
        public IEnumerable<SandwichedSwap> SandwichedSwaps { get; set; }
        public IEnumerable<Liquidation> Liquidations { get; set; }
        public IEnumerable<NftTrade> NftTrades { get; set; }
        public IEnumerable<PunkBid> PunkBids { get; set; }
        public IEnumerable<PunkBidAcceptance> PunkBidAcceptances { get; set; }
        public IEnumerable<PunkSnipe> PunkSnipes { get; set; }
        public IEnumerable<ZmBlock> ZmBlocks { get; set; }

        Dictionary<long, MEVBlock2> _mevBlocks = new Dictionary<long, MEVBlock2>();

        public static BlockProcess Load(long fromBlockNumber, long toBlockNumber)
        {
            BlockProcess bi = new BlockProcess();

            using (var db = new zeromevContext())
            {
                bi.Swaps = (from s in db.Swaps
                            where s.BlockNumber >= fromBlockNumber && s.BlockNumber <= toBlockNumber
                            orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                            select s).ToList();

                bi.Sandwiches = (from s in db.Sandwiches
                                 where s.BlockNumber >= fromBlockNumber && s.BlockNumber <= toBlockNumber
                                 select s).ToList();

                bi.SandwichedSwaps = (from s in db.SandwichedSwaps
                                      where s.BlockNumber >= fromBlockNumber && s.BlockNumber <= toBlockNumber
                                      select s).ToList();

                bi.Arbitrages = (from a in db.Arbitrages
                                 where a.BlockNumber >= fromBlockNumber && a.BlockNumber <= toBlockNumber
                                 select a).ToList();

                bi.Liquidations = (from s in db.Liquidations
                                   where s.BlockNumber >= fromBlockNumber && s.BlockNumber <= toBlockNumber
                                   orderby s.BlockNumber, s.TransactionHash, s.TraceAddress
                                   select s).ToList();

                bi.NftTrades = (from n in db.NftTrades
                                where n.BlockNumber >= fromBlockNumber && n.BlockNumber <= toBlockNumber
                                orderby n.BlockNumber, n.TransactionPosition, n.TraceAddress
                                select n).ToList();

                bi.PunkBids = (from p in db.PunkBids
                               where p.BlockNumber >= fromBlockNumber && p.BlockNumber <= toBlockNumber
                               orderby p.BlockNumber, p.TransactionHash, p.TraceAddress
                               select p).ToList();

                bi.PunkBidAcceptances = (from p in db.PunkBidAcceptances
                                         where p.BlockNumber >= fromBlockNumber && p.BlockNumber <= toBlockNumber
                                         orderby p.BlockNumber, p.TransactionHash, p.TraceAddress
                                         select p).ToList();

                bi.PunkSnipes = (from p in db.PunkSnipes
                                 where p.BlockNumber >= fromBlockNumber && p.BlockNumber <= toBlockNumber
                                 orderby p.BlockNumber, p.TransactionHash, p.TraceAddress
                                 select p).ToList();

                bi.ZmBlocks = (from z in db.ZmBlocks
                               where z.BlockNumber >= fromBlockNumber && z.BlockNumber <= toBlockNumber
                               orderby z.BlockNumber
                               select z).ToList();
            }

            return bi;
        }

        public void Process()
        {
            Tokens.Load();

            Sandwich? sandwich = null;
            ZMSwap? zmFrontrun = null;
            MEVFrontrun frontrun = null;
            int frontrunIndex = 0;
            string lastArbHash = null;

            MEVBlock2? mevBlock = null;
            List<DateTime>? arrivals = null;
            List<MEVSandwiched> sandwiched = new List<MEVSandwiched>();

            DEXs dexs = new DEXs();
            ZMDecimal? sandwichTotalUSD = 0;
            ZMDecimal? arbTotalUSD = 0;

            // swaps, sandwiches and arbs
            foreach (Swap s in Swaps)
            {
                // add swap
                if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals))
                    continue;

                if (s.TransactionPosition == null || s.Error != null) continue;
                var zmSwap = dexs.Add(s, arrivals[s.TransactionPosition.Value], out var pair);

                // sandwiches
                if (sandwich == null)
                {
                    sandwich = this.Sandwiches.FirstOrDefault<Sandwich>(x => { return x.FrontrunSwapTransactionHash == s.TransactionHash && Enumerable.SequenceEqual(x.FrontrunSwapTraceAddress, s.TraceAddress); });
                    if (sandwich != null)
                    {
                        // sandwich frontrun
                        frontrun = new MEVFrontrun(frontrunIndex, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                        zmFrontrun = zmSwap;
                    }
                    else
                    {
                        // arbitrages
                        var arb = this.Arbitrages.FirstOrDefault<Arbitrage>(x => { return x.TransactionHash == s.TransactionHash; });
                        if (arb != null)
                        {
                            if (arb.TransactionHash != lastArbHash)
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
                }
                else if (s.TransactionHash == sandwich.BackrunSwapTransactionHash && Enumerable.SequenceEqual(s.TraceAddress, sandwich.BackrunSwapTraceAddress))
                {
                    // sandwich backrun

                    // in/out tokens should be the same, but use distinct rates to be sure
                    ZMDecimal? victimLoss = (zmFrontrun.InAmount * zmFrontrun.InRateUSD) - (zmSwap.OutAmount * zmSwap.OutRateUSD);
                    Debug.WriteLine($"sandwich {victimLoss} usd");
                    sandwichTotalUSD += victimLoss;

                    // TODO add in USD amount and calculate the victimLoss in the class so we can display the calculations
                    // TODO then do liquidations
                    // TODO then NFT
                    var backrun = new MEVBackrun(s.TransactionPosition.Value, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap), victimLoss.Value.ToUSD());
                    mevBlock.Frontruns.Add(frontrun);
                    mevBlock.Sandwiched.AddRange(sandwiched);
                    mevBlock.Backruns.Add(backrun);

                    // look for the next
                    sandwich = null;
                    zmFrontrun = null;
                    sandwiched.Clear();
                }
                else
                {
                    // sandwiched
                    if (sandwiched.FindIndex(x => { return x.TxIndex == s.TransactionPosition; }) == -1)
                        sandwiched.Add(new MEVSandwiched(s.TransactionPosition, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap)));
                }

                //VictimImpact(zmSwap, pair); under development
            }

            // liquidations
            foreach (var l in Liquidations)
            {

            }

            // nft swaps
            foreach (var nft in NftTrades)
            {

            }

            // punk bids
            foreach (var pb in PunkBids)
            {

            }

            // punk snipes
            foreach (var pa in PunkBidAcceptances)
            {

            }

            // punk snipes
            foreach (var ps in PunkSnipes)
            {

            }

            foreach (var mb in _mevBlocks.Values)
            {
                Debug.WriteLine(mb.BlockNumber);
                foreach (var m in mb.Backruns) DebugMEV(m, mb);
                foreach (var m in mb.Sandwiched) DebugMEV(m, mb);
                foreach (var m in mb.Frontruns) DebugMEV(m, mb);
                foreach (var m in mb.Arbs) DebugMEV(m, mb);
                Debug.WriteLine("");
            }

            Debug.WriteLine($"{sandwichTotalUSD} sandwich victim loss");
            Debug.WriteLine($"{arbTotalUSD} arb victim loss");
        }

        private void DebugMEV(IMEV mev, MEVBlock2 mb)
        {
            Debug.WriteLine($"{mev.MEVType} {mev.MEVClass}");
            Debug.WriteLine(mev.ActionDetail(mb));
        }

        private void VictimImpact(ZMSwap zmSwap, Pair pair)
        {
            // TODO under development
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
        }

        private bool GetMEVBlock(long blockNumber, ref MEVBlock2? mevBlock, ref List<DateTime>? arrivals)
        {
            // only get new references if we have changed block number (iterate sorted by block number to minimize this)
            if (mevBlock == null || blockNumber != mevBlock.BlockNumber)
            {
                // get or create mev block
                if (!_mevBlocks.TryGetValue(blockNumber, out mevBlock))
                {
                    mevBlock = new MEVBlock2(blockNumber);
                    _mevBlocks.Add(blockNumber, mevBlock);
                }

                arrivals = null;
                ZmBlock zb = ZmBlocks.First<ZmBlock>(x => x.BlockNumber == blockNumber);
                if (zb == null)
                {
                    // TODO attempt to get and write arrivals on the fly
                }
                if (zb != null)
                {
                    var txData = Binary.Decompress(zb.TxData);
                    arrivals = Binary.ReadFirstSeenTxData(txData);
                    Debug.Assert(arrivals.Count == zb.TransactionCount);
                }
                else
                {
                    mevBlock = null;
                    return false;
                }
            }
            return true;
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

    public class Order : IComparable<Order>
    {
        public DateTime TimeOrder { get; set; }
        public BlockOrder BlockOrder;

        int IComparable<Order>.CompareTo(Order? other)
        {
            int r = this.TimeOrder.CompareTo(other.TimeOrder);
            if (r != 0) return r;
            return this.BlockOrder.Compare(other.BlockOrder);
        }

        public override string ToString()
        {
            return TimeOrder.ToString() + " " + BlockOrder.ToString();
        }
    }

    public class BlockOrder : IComparable<BlockOrder>
    {
        public long Blocknum;
        public int TxIndex;
        public int[] TraceAddress;

        public BlockOrder(long blocknum, int txIndex, int[] traceAddress)
        {
            Blocknum = blocknum;
            TxIndex = txIndex;
            TraceAddress = traceAddress;
        }

        public int Compare(BlockOrder? other)
        {
            if (other == null) return -1;

            // compare by block number
            int r;
            r = this.Blocknum.CompareTo(other.Blocknum);
            if (r != 0) return r;

            // then by transaction index
            r = this.TxIndex.CompareTo(other.TxIndex);
            if (r != 0) return r;

            // and finally by trace address

            // handle null and empty trace address values
            bool noTrace = (this.TraceAddress == null || this.TraceAddress.Length == 0);
            bool noOtherTrace = (other.TraceAddress == null || other.TraceAddress.Length == 0);
            if (noTrace && noOtherTrace) return 0;
            if (!noTrace && noOtherTrace) return 1;
            if (noTrace && !noOtherTrace) return -1;

            // compare trace address arrays directly now we know they both exist
            for (int i = 0; i < this.TraceAddress.Length; i++)
            {
                if (other.TraceAddress.Length < i) break;
                r = this.TraceAddress[i].CompareTo(other.TraceAddress[i]);
                if (r != 0) return r;
            }

            // if the are both equivalent as far as they go, the shorter one takes priority
            return this.TraceAddress.Length.CompareTo(other.TraceAddress.Length);
        }

        int IComparable<BlockOrder>.CompareTo(BlockOrder? other)
        {
            return Compare(other);
        }

        public override string ToString()
        {
            return $"{Blocknum} {TxIndex} [{string.Join(",", TraceAddress)}]";
        }
    }

    public class ZMSwap
    {
        // holds both time and block orderings
        public Order Order;

        // buy or sell
        public bool IsSell;

        // token symbols are smaller and easier to read than addresses (but they are not guaranteed to be unique, and should not be used as keys)
        public string SymbolA;
        public string SymbolB;

        // set if this swap is counted as contributing to another mev type (eg: arb or sandwich - if so, its MEV amount belongs to the parent)
        public MEVType ParentType;
        public BlockOrder? Parent;

        public ZMDecimal? ImpactDelta;

        // amounts are already adjusted by the token divisor, so rate calculations are reliable
        // ensure in/outs are never zero, the div by zero errors we will get if not are appropriate
        public ZMDecimal AmountA;
        public ZMDecimal AmountB;
        public ZMDecimal? MEVAmount; // 0 = neutral, >0 = positive <0 = negative, null = unclassified

        // determined directly from a USD stable-coin, or via ETH to a USD stable-coin after the swap. Determined within block order, so does not change. Set to 1 if it is already in USD, and null if unknown
        public ZMDecimal? ARateUSD;
        public ZMDecimal? BRateUSD;

        public ZMSwap(BlockOrder blockOrder, DateTime arrivalTime, bool isSell, ZMDecimal amountIn, ZMDecimal amountOut, string symbolIn, string symbolOut)
        {
            Order = new Order();
            Order.TimeOrder = arrivalTime;
            Order.BlockOrder = blockOrder;
            IsSell = isSell;

            if (!isSell)
            {
                AmountA = amountIn;
                AmountB = amountOut;
                SymbolA = symbolIn;
                SymbolB = symbolOut;
            }
            else
            {
                AmountA = amountOut;
                AmountB = amountIn;
                SymbolA = symbolOut;
                SymbolB = symbolIn;
            }
        }

        public ZMDecimal ExchangeRate()
        {
            return AmountB / AmountA;
        }

        public ZMDecimal InverseExchangeRate()
        {
            return AmountA / AmountB;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        public ZMDecimal InAmount
        {
            get
            {
                return IsSell ? AmountB : AmountA;
            }
        }

        public ZMDecimal OutAmount
        {
            get
            {
                return IsSell ? AmountA : AmountB;
            }
        }
        public ZMDecimal OrigExchangeRate
        {
            get
            {
                return IsSell ? InverseExchangeRate() : ExchangeRate();
            }
        }

        public ZMDecimal? InRateUSD
        {
            get
            {
                return IsSell ? BRateUSD : ARateUSD;
            }
        }

        public ZMDecimal? OutRateUSD
        {
            get
            {
                return IsSell ? ARateUSD : BRateUSD;
            }
        }
    }
}