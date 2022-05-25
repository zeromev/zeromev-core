using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class ClassifiedTrace
    {
        public DateTime? ClassifiedAt { get; set; }
        public string TransactionHash { get; set; } = null!;
        public long BlockNumber { get; set; }
        public string Classification { get; set; } = null!;
        public string TraceType { get; set; } = null!;
        public string? Protocol { get; set; }
        public string? AbiName { get; set; }
        public string? FunctionName { get; set; }
        public string? FunctionSignature { get; set; }
        public string? Inputs { get; set; }
        public string? FromAddress { get; set; }
        public string? ToAddress { get; set; }
        public BigInteger? Gas { get; set; }
        public BigInteger? Value { get; set; }
        public BigInteger? GasUsed { get; set; }
        public string? Error { get; set; }
        public int[] TraceAddress { get; set; } = null!;
        public int? TransactionPosition { get; set; }
    }
}
