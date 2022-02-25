using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class PunkSnipe
    {
        public DateTime? CreatedAt { get; set; }
        public long BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public string TraceAddress { get; set; } = null!;
        public string FromAddress { get; set; } = null!;
        public BigInteger PunkIndex { get; set; }
        public BigInteger MinAcceptancePrice { get; set; }
        public BigInteger AcceptancePrice { get; set; }
    }
}
