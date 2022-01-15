using System;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public class TxTimeHash : TxTime, IComparable<TxTimeHash>
    {
        public string TxHash { get; set; }

        public int CompareTo(TxTimeHash other)
        {
            return this.ArrivalTime.CompareTo(other.ArrivalTime);
        }
    }
}