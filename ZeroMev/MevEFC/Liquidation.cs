using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class Liquidation
    {
        public DateTime? CreatedAt { get; set; }
        public string LiquidatedUser { get; set; } = null!;
        public string LiquidatorUser { get; set; } = null!;
        public string DebtTokenAddress { get; set; } = null!;
        public decimal DebtPurchaseAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public string? Protocol { get; set; }
        public string TransactionHash { get; set; } = null!;
        public string TraceAddress { get; set; } = null!;
        public decimal BlockNumber { get; set; }
        public string? ReceivedTokenAddress { get; set; }
        public string? Error { get; set; }
    }
}
