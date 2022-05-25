using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class MevSummary
    {
        public BigInteger? BaseFeePerGas { get; set; }
        public BigInteger? BlockNumber { get; set; }
        public DateTime? BlockTimestamp { get; set; }
        public BigInteger? CoinbaseTransfer { get; set; }
        public DateTime? CreatedAt { get; set; }
        public BigInteger? GasPriceWithCoinbaseTransfer { get; set; }
        public BigInteger? GasUsed { get; set; }
        public BigInteger? GrossProfitUsd { get; set; }
        public string? MinerAddress { get; set; }
        public BigInteger? MinerPaymentUsd { get; set; }
        public string? Protocol { get; set; }
        public string? TransactionHash { get; set; }
        public string? Type { get; set; }
        public string? Error { get; set; }
    }
}
