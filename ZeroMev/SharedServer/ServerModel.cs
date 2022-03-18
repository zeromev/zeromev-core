using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public class ExtractorBlock
    {
        readonly public long BlockNumber;
        readonly public short ExtractorIndex;
        readonly public DateTime BlockTime;
        readonly public DateTime ExtractorStartTime;
        readonly public long ArrivalCount;
        readonly public int PendingCount;
        readonly public byte[] RawTxTimes;

        List<TxTime> _txTimes = null;

        public ExtractorPoP Extractor
        {
            get
            {
                return (ExtractorPoP)ExtractorIndex;
            }
        }

        public List<TxTime> TxTimes
        {
            get
            {
                if (_txTimes != null) return _txTimes;
                if (RawTxTimes != null)
                {
                    byte[] uncomp = Binary.Decompress(RawTxTimes);
                    return Binary.ReadTxData(uncomp);
                }
                return null;
            }
        }

        public List<TxTime> TxTimesDataFix()
        {
            // call before calling TxTimes to convert local timezone arrival times to UTC so they can be written back as UTC
            if (_txTimes != null) return _txTimes;
            if (RawTxTimes != null)
            {
                byte[] uncomp = Binary.Decompress(RawTxTimes);
                _txTimes = Binary.ReadTxData(uncomp);

                // convert timezones until we've done a datafix
                for (int i = 0; i < _txTimes.Count; i++)
                    _txTimes[i].ArrivalTime = Time.ToUTC(this.Extractor, DateTime.SpecifyKind(_txTimes[i].ArrivalTime, DateTimeKind.Unspecified));
                return _txTimes;
            }
            return null;
        }

        public ExtractorBlock(long blockNumber, short extractorIndex, DateTime blockTime, DateTime extractorStartTime, long arrivalCount, int pendingCount, List<TxTimeHash> txTimes)
        {
            BlockNumber = blockNumber;
            ExtractorIndex = extractorIndex;
            BlockTime = blockTime;
            ExtractorStartTime = extractorStartTime;
            ArrivalCount = arrivalCount;
            PendingCount = pendingCount;
            _txTimes = txTimes.Cast<TxTime>().ToList();
        }

        public ExtractorBlock(long blockNumber, short extractorIndex, DateTime blockTime, DateTime extractorStartTime, long arrivalCount, int pendingCount, byte[] compTxTimes)
        {
            BlockNumber = blockNumber;
            ExtractorIndex = extractorIndex;
            BlockTime = blockTime;
            ExtractorStartTime = extractorStartTime;
            ArrivalCount = arrivalCount;
            PendingCount = pendingCount;
            RawTxTimes = compTxTimes;
        }
    }
}