using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class Transfer
    {
        public DateTime? CreatedAt { get; set; }
        public long BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public int[] TraceAddress { get; set; } = null!;
        public string? Protocol { get; set; }
        public string FromAddress { get; set; } = null!;
        public string ToAddress { get; set; } = null!;
        public string TokenAddress { get; set; } = null!;
        public BigInteger Amount { get; set; }
        public string? Error { get; set; }
    }
}