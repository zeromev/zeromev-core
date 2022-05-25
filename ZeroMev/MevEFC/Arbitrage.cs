using System;
using System.Collections.Generic;
using System.Numerics;

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
        public long BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public BigInteger StartAmount { get; set; }
        public BigInteger EndAmount { get; set; }
        public BigInteger ProfitAmount { get; set; }
        public string? Error { get; set; }
        public string[]? Protocols { get; set; }

        public virtual IEnumerable<ArbitrageSwap> ArbitrageSwaps { get; set; }
    }
}
