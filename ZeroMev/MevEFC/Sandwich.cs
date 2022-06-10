using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class Sandwich
    {
        public Sandwich()
        {
            SandwichedSwaps = new HashSet<SandwichedSwap>();
        }

        public string Id { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public long BlockNumber { get; set; }
        public string SandwicherAddress { get; set; } = null!;
        public string FrontrunSwapTransactionHash { get; set; } = null!;
        public int[] FrontrunSwapTraceAddress { get; set; } = null!;
        public string BackrunSwapTransactionHash { get; set; } = null!;
        public int[] BackrunSwapTraceAddress { get; set; } = null!;
        public string? ProfitTokenAddress { get; set; }
        public BigInteger? ProfitAmount { get; set; }

        public virtual ICollection<SandwichedSwap> SandwichedSwaps { get; set; }
    }
}
