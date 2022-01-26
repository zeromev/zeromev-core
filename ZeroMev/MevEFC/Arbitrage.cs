using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class Arbitrage
    {
        public Arbitrage()
        {
            ArbitrageSwaps = new HashSet<ArbitrageSwap>();
        }

        public string Id { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public string AccountAddress { get; set; } = null!;
        public string ProfitTokenAddress { get; set; } = null!;
        public decimal BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public decimal StartAmount { get; set; }
        public decimal EndAmount { get; set; }
        public decimal ProfitAmount { get; set; }
        public string? Error { get; set; }

        public virtual ICollection<ArbitrageSwap> ArbitrageSwaps { get; set; }
    }
}
