using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class ZmBlock
    {
        public decimal BlockNumber { get; set; }
        public int TransactionCount { get; set; }
        public DateTime? BlockTime { get; set; }
        public byte[]? TxData { get; set; }
    }
}
