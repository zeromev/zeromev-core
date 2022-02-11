using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroMev.MevEFC;
using ZeroMev.Shared;

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
        public IEnumerable<Liquidation> Liquidations { get; set; }
        public IEnumerable<NftTrade> NftTrades { get; set; }
        public IEnumerable<PunkSnipe> PunkSnipes { get; set; }

        public static BlockInput Build(long fromBlockNumber, long toBlockNumber)
        {
            // TODO 
            return null;
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
        public void Add(Swap swap, DateTime arrivalTime)
        {
            if (swap == null) return;

            // build dex key
            string? key = DEX.BuildKey(swap);
            if (key == null) return;

            // add new or get existing
            DEX? dex;
            if (!this.TryGetValue(key, out dex))
            {
                dex = new DEX(swap.AbiName, swap.Protocol);
                this.Add(key, dex);
            }

            // add the swap to the dex
            if (dex != null)
                dex.Add(swap, arrivalTime);
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

        public void Add(Swap swap, DateTime arrivalTime)
        {
            // TODO
            bool isSell;
            var pair = GetOrAddPair(swap.TokenInAddress, swap.TokenOutAddress, out isSell);
            BlockOrder blockOrder = new BlockOrder((long)swap.BlockNumber, (int)swap.TransactionPosition.Value, swap.TraceAddress); // if transaction position is ever null, we want it to raise an exception
            pair.Add(swap, blockOrder, arrivalTime, isSell);
        }

        private Pair GetOrAddPair(string tokenIn, string tokenOut, out bool IsSell)
        {
            // sort in/out tokens to get a single pair containing both buys and sells
            string tokenA, tokenB;
            Pair.ConformPair(tokenIn, tokenOut, out tokenA, out tokenB, out IsSell);

            // create and add if required, then return
            Pair? pair;
            string key = tokenA + tokenB;
            if (!this.TryGetValue(key, out pair))
            {
                pair = new Pair(this, tokenA, tokenB);
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

    public enum Currency
    {
        USD,
        ETH,
        BTC
    }

    public class Pair
    {
        public DEX Parent { get; private set; }
        public string TokenA { get; private set; }
        public string TokenB { get; private set; }

        // we always progress by block time, so don't need a list
        public ZMSwap? CurrentSwap = null;
        public ZMSwap? PreviousSwap = null;

        // we need to hop around in arrival time order, so need a sorted collection
        public readonly SortedList<Order, ZMSwap> TimeOrder = new SortedList<Order, ZMSwap>();

        // use latest exchange rates for each pair by block/index time to calculate MEV impacts in dollar terms at the moment they executed
        // TODO xrates need looking at again
        public ZMDecimal[] XRate = new ZMDecimal[Enum.GetValues(typeof(Currency)).Length];

        private static Dictionary<string, Currency> XRateTokens = new Dictionary<string, Currency>
        {
            // USD
            {"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48" , Currency.USD },  // USDC
            {"0x6b175474e89094c44da98b954eedeac495271d0f" , Currency.USD },  // DAI
            {"0xdac17f958d2ee523a2206206994597c13d831ec7" , Currency.USD },  // TUSD
            {"0x4fabb145d64652a948d72533023f6e7a623c7c53" , Currency.USD },  // BUSD
            {"0x0000000000085d4780B73119b644AE5ecd22b376" , Currency.USD },  // TrueUSD
            {"0x056fd409e1d7a124bd7017459dfea2f387b6d5cd" , Currency.USD },  // GUSD

            // ETH
            {"0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2" , Currency.ETH },  // wETH

            // BTC
            {"0x2260fac5e5542a773aa44fbcfedf7c193bc2c599" , Currency.BTC },  // wBTC
            {"0xeb4c2781e4eba804ce9a9803c67d0893436bb27d" , Currency.BTC }  // renBTC
        };

        public Pair(DEX parent, string tokenA, string tokenB)
        {
            Parent = parent;
            TokenA = tokenA;
            TokenB = tokenB;
        }

        // add must be called in block order for 
        public void Add(Swap swap, BlockOrder blockOrder, DateTime arrivalTime, bool isSell)
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

            // update block order (represented as current and previous swaps)
            PreviousSwap = CurrentSwap;
            CurrentSwap = zmSwap;

            // update time order (a sorted collection)
            TimeOrder.TryAdd(zmSwap.Order, zmSwap);

            // TODO update to use Tokens static
            UpdateExchangeRate(zmSwap);
        }

        private void UpdateExchangeRate(ZMSwap swap)
        {
            // avoid div by zero and save cpu
            if (swap.AmountA == 0 || swap.AmountB == 0)
                return;

            Currency currency;
            if (XRateTokens.TryGetValue(TokenB, out currency))
            {
                // take any conversion as our last exchange rate
                XRate[(int)currency] = swap.ExchangeRate();
            }
            else if (XRateTokens.TryGetValue(TokenA, out currency))
            {
                // invert the rate when the tokens are reversed
                XRate[(int)currency] = swap.InverseExchangeRate();
            }
        }

        public ZMDecimal LastXRate(Currency currency)
        {
            return XRate[(int)currency];
        }

        // sort in/out tokens to get a single pair which can contain both buys and sells
        public static void ConformPair(string tokenIn, string tokenOut, out string tokenA, out string tokenB, out bool IsSell)
        {
            if (string.Compare(tokenIn, tokenOut) <= 0)
            {
                tokenA = tokenIn;
                tokenB = tokenOut;
                IsSell = false;
            }
            else
            {
                tokenA = tokenOut;
                tokenB = tokenIn;
                IsSell = true;
            }
        }

        public override string ToString()
        {
            return Tokens.GetPairName(TokenA, TokenB);
        }
    }
}