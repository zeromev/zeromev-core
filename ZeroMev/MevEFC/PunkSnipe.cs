using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class PunkSnipe
    {
        public DateTime? CreatedAt { get; set; }
        public decimal BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public string TraceAddress { get; set; } = null!;
        public string FromAddress { get; set; } = null!;
        public decimal PunkIndex { get; set; }
        public decimal MinAcceptancePrice { get; set; }
        public decimal AcceptancePrice { get; set; }
    }
}
