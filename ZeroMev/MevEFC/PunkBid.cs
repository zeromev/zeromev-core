using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class PunkBid
    {
        public DateTime? CreatedAt { get; set; }
        public long BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public string TraceAddress { get; set; } = null!;
        public string FromAddress { get; set; } = null!;
        public decimal PunkIndex { get; set; }
        public decimal Price { get; set; }
    }
}