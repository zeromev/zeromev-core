﻿using System;
using System.Collections.Generic;

namespace ZeroMev.MevEFC
{
    public partial class LatestBlockUpdate
    {
        public long BlockNumber { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
