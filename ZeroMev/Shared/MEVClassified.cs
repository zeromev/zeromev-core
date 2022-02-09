using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZeroMev.Shared
{
    public class Order : IComparable<Order>
    {
        public DateTime TimeOrder { get; set; }
        public BlockOrder BlockOrder;

        int IComparable<Order>.CompareTo(Order? other)
        {
            int r = this.TimeOrder.CompareTo(other.TimeOrder);
            if (r != 0) return r;
            return ((IComparable)this.BlockOrder).CompareTo(other.BlockOrder);
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
        // holds both time and block orderings
        public Order Order;

        // buy or sell
        public bool IsSell;

        // indexes not references so they can be easily stored
        // when these change compared to the time ordered list, we know the time order has changed based on new arrivals
        public BlockOrder NextByTime;
        public BlockOrder PrevByTime;

        // set if this swap is counted as contributing to another mev type (eg: arb or sandwich - if so, its MEV amount belongs to the parent)
        public MEVType ParentType;
        public BlockOrder? Parent;

        // amounts are already adjusted by the token divisor, so rate calculations are reliable
        // ensure in/outs are never zero, the div by zero errors we will get if not are appropriate
        public ZMDecimal AmountIn;
        public ZMDecimal AmountOut;
        public ZMDecimal? MEVAmount; // 0 = neutral, >0 = positive <0 = negative, null = unclassified

        // determined directly from a USD stable-coin, or via ETH to a USD stable-coin. Determined within block order, so does not change. Set to 1 if it is already in USD, and null if unknown
        public ZMDecimal? InRateUSD;
        public ZMDecimal? OutRateUSD;

        public ZMSwap(BlockOrder blockOrder, DateTime arrivalTime, bool isSell, ZMDecimal amountIn, ZMDecimal amountOut)
        {
            Order = new Order();
            Order.TimeOrder = arrivalTime;
            Order.BlockOrder = blockOrder;
            IsSell = isSell;
            AmountIn = amountIn;
            AmountOut = amountOut;
        }

        public ZMDecimal ExchangeRate()
        {
            if (IsSell)
                return AmountIn / AmountOut;
            return AmountOut / AmountIn;
        }

        public ZMDecimal InverseExchangeRate()
        {
            if (IsSell)
                return AmountOut / AmountIn;
            return AmountIn / AmountOut;
        }

        public ZMDecimal ImpactDelta(ZMSwap previous)
        {
            return ExchangeRate() - previous.ExchangeRate();
        }

        public ZMDecimal ImpactPercent(ZMSwap previous)
        {
            return ImpactDelta(previous) / ExchangeRate();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}