using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class Price
    {
        public DateTime Timestamp { get; set; }
        public BigInteger UsdPrice { get; set; }
        public string TokenAddress { get; set; } = null!;
    }
}
