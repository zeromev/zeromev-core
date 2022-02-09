using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class ZmToken
    {
        public string Address { get; set; } = null!;
        public string? Name { get; set; }
        public int? Decimals { get; set; }
        public string? Symbol { get; set; }
        public string? Owner { get; set; }
        public string? Image { get; set; }
        public string? Website { get; set; }
        public string? Facebook { get; set; }
        public string? Telegram { get; set; }
        public string? Twitter { get; set; }
        public string? Reddit { get; set; }
        public string? Coingecko { get; set; }
    }
}
