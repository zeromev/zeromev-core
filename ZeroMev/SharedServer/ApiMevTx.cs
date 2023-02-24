using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public class ApiMevTx
    {
        public long block_number;
        public int tx_index;
        public MEVType mev_type;
        public string protocol;
        public decimal? user_loss_usd;
        public decimal? extractor_profit_usd;
        public decimal? swap_volume_usd;
        public int? swap_count;
        public decimal? extractor_swap_volume_usd;
        public int? extractor_swap_count;
        public float? imbalance;
        public string? address_from;
        public string? address_to;
        public DateTime? arrival_time_us;
        public DateTime? arrival_time_eu;
        public DateTime? arrival_time_as;
    }
}