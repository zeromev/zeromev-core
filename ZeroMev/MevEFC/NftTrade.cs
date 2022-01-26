using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class NftTrade
    {
        public DateTime? CreatedAt { get; set; }
        public string AbiName { get; set; } = null!;
        public string TransactionHash { get; set; } = null!;
        public decimal TransactionPosition { get; set; }
        public decimal BlockNumber { get; set; }
        public string TraceAddress { get; set; } = null!;
        public string Protocol { get; set; } = null!;
        public string? Error { get; set; }
        public string SellerAddress { get; set; } = null!;
        public string BuyerAddress { get; set; } = null!;
        public string PaymentTokenAddress { get; set; } = null!;
        public decimal PaymentAmount { get; set; }
        public string CollectionAddress { get; set; } = null!;
        public decimal TokenId { get; set; }
    }
}
