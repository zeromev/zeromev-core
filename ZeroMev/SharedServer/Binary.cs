using System;
using System.Collections.Generic;
using ZeroMev.Shared;
using System.IO;
using System.IO.Compression;

namespace ZeroMev.SharedServer
{
    public static class Binary
    {
        public static byte[] WriteTxData(List<TxTime> tts)
        {
            byte[] txData = new byte[tts.Count * 16];

            int index = 0;
            if (BitConverter.IsLittleEndian)
            {
                // use little endian for binary data
                foreach (var tt in tts)
                {
                    ToBytes(tt.ArrivalTime.Ticks, txData, index);
                    index += 8;
                    ToBytes(tt.ArrivalBlockNumber, txData, index);
                    index += 8;
                }
            }
            else
            {
                // convert big endian to little endian
                foreach (var tt in tts)
                {
                    ToBytes(tt.ArrivalTime.Ticks, txData, index);
                    Array.Reverse(txData, index, 8);
                    index += 8;
                    ToBytes(tt.ArrivalBlockNumber, txData, index);
                    Array.Reverse(txData, index, 8);
                    index += 8;
                }
            }

            return txData;
        }

        public static List<TxTime> ReadTxData(byte[] txData)
        {
            int len = txData.Length / 16;
            List<TxTime> tts = new List<TxTime>(len);
            int i = 0;

            if (BitConverter.IsLittleEndian)
            {
                // use little endian for binary data
                while (i < txData.Length)
                {
                    TxTime tt = new TxTime();
                    tt.ArrivalTime = DateTime.FromBinary(BitConverter.ToInt64(txData, i));
                    i += 8;
                    tt.ArrivalBlockNumber = BitConverter.ToInt64(txData, i);
                    i += 8;
                    tts.Add(tt);
                }
            }
            else
            {
                // convert big endian to little endian
                while (i < txData.Length)
                {
                    TxTime tt = new TxTime();
                    Array.Reverse(txData, i, 8);
                    tt.ArrivalTime = DateTime.FromBinary(BitConverter.ToInt64(txData, i));
                    i += 8;
                    Array.Reverse(txData, i, 8);
                    tt.ArrivalBlockNumber = BitConverter.ToInt64(txData, i);
                    i += 8;
                    tts.Add(tt);
                }
            }

            return tts;
        }

        public static byte[] WriteFirstSeenTxData(ZMView zv)
        {
            if (zv == null || zv.Txs == null)
                return null;

            if (zv.Txs.Length == 0)
                return new byte[] { };

            byte[] txData = new byte[zv.Txs.Length * 8];

            int index = 0;
            if (BitConverter.IsLittleEndian)
            {
                // use little endian for binary data
                foreach (var t in zv.Txs)
                {
                    ToBytes(t.ArrivalMin.Ticks, txData, index);
                    index += 8;
                }
            }
            else
            {
                // convert big endian to little endian
                foreach (var t in zv.Txs)
                {
                    ToBytes(t.ArrivalMin.Ticks, txData, index);
                    Array.Reverse(txData, index, 8);
                    index += 8;
                }
            }

            return txData;
        }

        public static List<DateTime> ReadFirstSeenTxData(byte[] txData)
        {
            int len = txData.Length / 8;
            List<DateTime> tts = new List<DateTime>(len);
            int i = 0;

            if (BitConverter.IsLittleEndian)
            {
                // use little endian for binary data
                while (i < txData.Length)
                {
                    tts.Add(DateTime.FromBinary(BitConverter.ToInt64(txData, i)));
                    i += 8;
                }
            }
            else
            {
                // convert big endian to little endian
                while (i < txData.Length)
                {
                    Array.Reverse(txData, i, 8);
                    tts.Add(DateTime.FromBinary(BitConverter.ToInt64(txData, i)));
                    i += 8;
                }
            }

            return tts;
        }

        static unsafe void ToBytes(long value, byte[] array, int offset)
        {
            fixed (byte* ptr = &array[offset])
                *(long*)ptr = value;
        }

        public static byte[] Compress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return compressedStream.ToArray();
            }
        }
        public static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }
    }
}