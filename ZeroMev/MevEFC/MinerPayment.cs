using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class MinerPayment
    {
        public DateTime? CreatedAt { get; set; }
        public decimal BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public decimal TransactionIndex { get; set; }
        public string MinerAddress { get; set; } = null!;
        public decimal CoinbaseTransfer { get; set; }
        public decimal BaseFeePerGas { get; set; }
        public decimal GasPrice { get; set; }
        public decimal GasPriceWithCoinbaseTransfer { get; set; }
        public decimal GasUsed { get; set; }
        public string? TransactionToAddress { get; set; }
        public string? TransactionFromAddress { get; set; }
    }
}
