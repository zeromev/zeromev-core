using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class Swap
    {
        public DateTime? CreatedAt { get; set; }
        public string AbiName { get; set; } = null!;
        public string TransactionHash { get; set; } = null!;
        public decimal BlockNumber { get; set; }
        public string? Protocol { get; set; }
        public string ContractAddress { get; set; } = null!;
        public string FromAddress { get; set; } = null!;
        public string ToAddress { get; set; } = null!;
        public string TokenInAddress { get; set; } = null!;
        public decimal TokenInAmount { get; set; }
        public string TokenOutAddress { get; set; } = null!;
        public decimal TokenOutAmount { get; set; }
        public int[] TraceAddress { get; set; } = null!;
        public string? Error { get; set; }
        public decimal? TransactionPosition { get; set; }
    }
}
