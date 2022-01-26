using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class ZmTime
    {
        public string TransactionHash { get; set; } = null!;
        public decimal BlockNumber { get; set; }
        public decimal TransactionPosition { get; set; }
        public DateTime? ArrivalTime { get; set; }
    }
}
