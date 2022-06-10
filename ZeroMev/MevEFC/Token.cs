using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZeroMev.MevEFC
{
    public partial class Token
    {
        public string TokenAddress { get; set; } = null!;
        public BigInteger Decimals { get; set; }
    }
}
