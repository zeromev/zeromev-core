using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class PunkBidAcceptance
    {
        public DateTime? CreatedAt { get; set; }
        public decimal BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public string TraceAddress { get; set; } = null!;
        public string FromAddress { get; set; } = null!;
        public decimal PunkIndex { get; set; }
        public decimal MinPrice { get; set; }
    }
}
