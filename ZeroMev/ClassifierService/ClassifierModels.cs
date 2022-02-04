using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroMev.MevEFC;

namespace ZeroMev.ClassifierService
{
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

    public class DEX: Dictionary<string, Pair>
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
                pair = new Pair(tokenA, tokenB);
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

    public class Pair
    {
        public string TokenA;
        public string TokenB;
        public BigInteger? LastExchangeRate = null; // we use latest exchange rate for each pair by block/index time to calculate MEV impacts in dollar terms at the moment they executed

        public readonly SortedList<BlockOrder, ZMSwap> BlockOrder = new SortedList<BlockOrder, ZMSwap>();
        public readonly SortedList<long, ZMSwap> TimeOrder = new SortedList<long, ZMSwap>();

        private static string[] USDtokens = new string[]
        {
            "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", // USDC
            "0x6B175474E89094C44Da98b954EedeAC495271d0F", // DAI
            "0xdac17f958d2ee523a2206206994597c13d831ec7", // TUSD
            "0x4fabb145d64652a948d72533023f6e7a623c7c53", // BUSD
            "0x0000000000085d4780B73119b644AE5ecd22b376", // TrueUSD
            "0x056fd409e1d7a124bd7017459dfea2f387b6d5cd"  // GUSD
        };

        public Pair(string tokenA, string tokenB)
        {
            TokenA = tokenA;
            TokenB = tokenB;
        }

        // add must be called in block order for 
        public void Add(Swap swap, BlockOrder blockOrder, DateTime arrivalTime, bool isSell)
        {
            // create a smaller footprint zeromev swap with our timing data
            ZMSwap zmSwap = new ZMSwap(blockOrder, arrivalTime, isSell, swap.TokenInAmount, swap.TokenOutAmount);
            zmSwap.BlockOrder = blockOrder;

            // add it to both the block and time ordered lists
            BlockOrder.TryAdd(blockOrder, zmSwap);
            TimeOrder.TryAdd(arrivalTime.Ticks, zmSwap);

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

            // take any conversion to or from a USD stable coin as our last exchange rate
            if (USDtokens.Contains<string>(TokenB))
            {
                // Other:USD
                LastExchangeRate = swap.ExchangeRate();
            }
            else if (USDtokens.Contains<string>(TokenA))
            {
                // invert the rate for USD:Other pairs
                LastExchangeRate = swap.InverseExchangeRate();
            }
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

        int IComparable<BlockOrder>.CompareTo(BlockOrder? other)
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

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class ZMSwap
    {
        public BlockOrder BlockOrder;
        public DateTime ArrivalTime;
        public bool IsSell;
        public BigInteger AmountIn; // ensure in/outs are never 0, the div by zero errors we will get are appropriate if not
        public BigInteger AmountOut;

        public ZMSwap(BlockOrder blockOrder, DateTime arrivalTime, bool isSell, BigInteger amountIn, BigInteger amountOut)
        {
            BlockOrder = blockOrder;
            ArrivalTime = arrivalTime;
            IsSell = isSell;
            AmountIn = amountIn;
            AmountOut = amountOut;
        }

        public long TimeOrderKey()
        {
            return ArrivalTime.Ticks;
        }

        public BigInteger ExchangeRate()
        {
            if (IsSell)
                return AmountIn / AmountOut;
            return AmountOut / AmountIn;
        }

        public BigInteger InverseExchangeRate()
        {
            if (IsSell)
                return AmountOut / AmountIn;
            return AmountIn / AmountOut;
        }

        public BigInteger ImpactDelta(ZMSwap previous)
        {
            return ExchangeRate() - previous.ExchangeRate();
        }

        public BigInteger ImpactPercent(ZMSwap previous)
        {
            return ImpactDelta(previous) / ExchangeRate();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}