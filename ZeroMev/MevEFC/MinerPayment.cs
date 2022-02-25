using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class MinerPayment
    {
        public DateTime? CreatedAt { get; set; }
        public long BlockNumber { get; set; }
        public string TransactionHash { get; set; } = null!;
        public int TransactionIndex { get; set; }
        public string MinerAddress { get; set; } = null!;
        public BigInteger CoinbaseTransfer { get; set; }
        public BigInteger BaseFeePerGas { get; set; }
        public BigInteger GasPrice { get; set; }
        public BigInteger GasPriceWithCoinbaseTransfer { get; set; }
        public BigInteger GasUsed { get; set; }
        public string? TransactionToAddress { get; set; }
        public string? TransactionFromAddress { get; set; }
    }
}
