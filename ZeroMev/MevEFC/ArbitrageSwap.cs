using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class ArbitrageSwap
    {
        public DateTime? CreatedAt { get; set; }
        public string ArbitrageId { get; set; } = null!;
        public string SwapTransactionHash { get; set; } = null!;
        public int[] SwapTraceAddress { get; set; } = null!;

        public virtual Arbitrage Arbitrage { get; set; } = null!;
    }
}
