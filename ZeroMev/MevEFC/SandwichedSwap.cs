using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class SandwichedSwap
    {
        public DateTime? CreatedAt { get; set; }
        public string SandwichId { get; set; } = null!;
        public decimal BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public int[] TraceAddress { get; set; } = null!;

        public virtual Sandwich Sandwich { get; set; } = null!;
    }
}
