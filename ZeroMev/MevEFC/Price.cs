using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class Price
    {
        public DateTime Timestamp { get; set; }
        public decimal UsdPrice { get; set; }
        public string TokenAddress { get; set; } = null!;
    }
}
