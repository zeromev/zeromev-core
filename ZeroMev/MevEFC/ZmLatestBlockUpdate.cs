using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class ZmLatestBlockUpdate
    {
        public decimal BlockNumber { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
