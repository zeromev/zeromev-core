using System;
using System.Collections.Generic;

namespace ZeroMev.SharedServer
{
    public class FBTx : IComparable<FBTx>
    {
        public int tx_index { get; set; }
        public int bundle_index { get; set; }
        public ulong block_number { get; set; }
        public string transaction_hash { get; set; }
        public string eoa_address { get; set; }
        public string to_address { get; set; }
        public ulong gas_used { get; set; }
        public string gas_price { get; set; }
        public string coinbase_transfer { get; set; }
        public string total_miner_reward { get; set; }

        public int CompareTo(FBTx other)
        {
            if (this.bundle_index != other.bundle_index)
                return this.bundle_index.CompareTo(other.bundle_index);
            return this.tx_index.CompareTo(other.tx_index);
        }
    }

    public class FBBlock
    {
        public long block_number { get; set; }
        public string miner_reward { get; set; }
        public string miner { get; set; }
        public string coinbase_transfers { get; set; }
        public ulong gas_used { get; set; }
        public string gas_price { get; set; }
        public List<FBTx> transactions { get; set; }
    }

    public class FBRoot
    {
        public List<FBBlock> blocks { get; set; }
        public long latest_block_number { get; set; }
    }
}