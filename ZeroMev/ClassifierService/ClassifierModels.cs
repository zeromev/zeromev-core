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
    // not threadsafe (by design)
    public static class Tokens
    {
        private static Dictionary<string, ZmToken> _tokens = null;

        public static void Load()
        {
            Dictionary<string, ZmToken> newTokens = new Dictionary<string, ZmToken>();

            using (var db = new zeromevContext())
            {
                var tokens = (from t in db.ZmTokens
                              where (t.Symbol != null && t.Symbol != "???")
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
            if (_tokens.TryGetValue(tokenAddress, out ZmToken? token))
                return token;
            return null;
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

        public readonly SortedList<BlockOrder, ZMSwap> BlockOrder = new SortedList<BlockOrder, ZMSwap>();
        public readonly SortedList<Order, ZMSwap> TimeOrder = new SortedList<Order, ZMSwap>();

        // use latest exchange rates for each pair by block/index time to calculate MEV impacts in dollar terms at the moment they executed
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
            ZMDecimal tokenIn = (ZMDecimal)swap.TokenInAmount;
            ZMDecimal tokenOut = (ZMDecimal)swap.TokenOutAmount;

            // TODO store decimals as PoW ZMDecimal that I can divide by

            // USDC
            if (swap.TokenInAddress == "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48") tokenIn /= ZMDecimal.Pow(10, 6);
            if (swap.TokenOutAddress == "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48") tokenOut /= ZMDecimal.Pow(10, 6);

            // WETH
            if (swap.TokenInAddress == "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2") tokenIn /= ZMDecimal.Pow(10, 18);
            if (swap.TokenOutAddress == "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2") tokenOut /= ZMDecimal.Pow(10, 18);

            // hex
            if (swap.TokenInAddress == "0x2b591e99afe9f32eaa6214f7b7629768c40eeb39") tokenIn /= ZMDecimal.Pow(10, 8);
            if (swap.TokenOutAddress == "0x2b591e99afe9f32eaa6214f7b7629768c40eeb39") tokenOut /= ZMDecimal.Pow(10, 8);

            // link
            if (swap.TokenInAddress == "0x514910771af9ca656af840dff83e8264ecf986ca") tokenIn /= ZMDecimal.Pow(10, 18);
            if (swap.TokenOutAddress == "0x514910771af9ca656af840dff83e8264ecf986ca") tokenOut /= ZMDecimal.Pow(10, 18);

            // uni
            if (swap.TokenInAddress == "0x1f9840a85d5af5bf1d1762f925bdaddc4201f984") tokenIn /= ZMDecimal.Pow(10, 18);
            if (swap.TokenOutAddress == "0x1f9840a85d5af5bf1d1762f925bdaddc4201f984") tokenOut /= ZMDecimal.Pow(10, 18);

            // LINK -> USDC
            if (swap.TokenInAddress == "0x514910771af9ca656af840dff83e8264ecf986ca" && swap.TokenOutAddress == "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48")
            {
                Console.WriteLine("");
            }

            // USDC -> LINK
            if (swap.TokenInAddress == "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48" && swap.TokenOutAddress == "0x514910771af9ca656af840dff83e8264ecf986ca")
            {
                Console.WriteLine("");
            }

            // TODO if the tokenIn/Out are outside the decimal integer range (or x100 close to being), we will need to skip them

            // create a smaller footprint zeromev swap with our timing data
            ZMSwap zmSwap = new ZMSwap(blockOrder, arrivalTime, isSell, tokenIn, tokenOut);

            // add it to both the block and time ordered lists
            BlockOrder.TryAdd(zmSwap.Order.BlockOrder, zmSwap);
            TimeOrder.TryAdd(zmSwap.Order, zmSwap);

            // usually we will add swaps in block/index order and so can calculate the latest exchange rate as we go
            // if for some reason we go back and add swaps, do not update the exchange rate as it will be out of date
            if (object.ReferenceEquals(BlockOrder.Values[BlockOrder.Count - 1], zmSwap))
                UpdateExchangeRate(zmSwap);
        }

        private void UpdateExchangeRate(ZMSwap swap)
        {
            // avoid div by zero and save cpu
            if (swap.AmountIn == 0 || swap.AmountOut == 0)
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

            //if (TokenA == "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48") Console.WriteLine($"USDC {currency} {swap.IsSell} {XRate[(int)currency]}");
            //if (TokenA == "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2") Console.WriteLine($"WETH {currency} {swap.IsSell} {XRate[(int)currency]}");

            if (TokenA == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" || TokenB == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")
                return;

            string dex = this.Parent.AbiName + this.Parent.Protocol;
            if (TokenA == "0x2b591e99afe9f32eaa6214f7b7629768c40eeb39") Console.WriteLine($"HEX {currency} {swap.IsSell} {XRate[(int)currency]} {dex}");
            if (TokenA == "0x514910771af9ca656af840dff83e8264ecf986ca")
            {
                Console.WriteLine($"LINK {currency} {swap.IsSell} {XRate[(int)currency]} {dex}");
                if (dex == "BancorNetworkbancor") Console.Write("");
                if (XRate[(int)currency] > 20000) Console.Write("");
            }
            if (TokenA == "0x1f9840a85d5af5bf1d1762f925bdaddc4201f984") Console.WriteLine($"UNI {currency} {swap.IsSell} {XRate[(int)currency]} {dex}");
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
            return JsonSerializer.Serialize(this);
        }
    }
}