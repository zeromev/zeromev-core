using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class ClassifiedTrace
    {
        public DateTime? ClassifiedAt { get; set; }
        public string TransactionHash { get; set; } = null!;
        public decimal BlockNumber { get; set; }
        public string Classification { get; set; } = null!;
        public string TraceType { get; set; } = null!;
        public string? Protocol { get; set; }
        public string? AbiName { get; set; }
        public string? FunctionName { get; set; }
        public string? FunctionSignature { get; set; }
        public string? Inputs { get; set; }
        public string? FromAddress { get; set; }
        public string? ToAddress { get; set; }
        public decimal? Gas { get; set; }
        public decimal? Value { get; set; }
        public decimal? GasUsed { get; set; }
        public string? Error { get; set; }
        public int[] TraceAddress { get; set; } = null!;
        public decimal? TransactionPosition { get; set; }
    }
}
