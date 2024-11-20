using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using Npgsql;

namespace ZeroMev.Test
{
    [TestClass]
    public class DBTest
    {
        [TestMethod]
        public async Task ReadLatestMevBlock()
        {
            long? b = await DB.ReadLatestMevBlock();
            Assert.IsNotNull(b);
        }

        [TestMethod]
        public async Task ReadMevBlocks()
        {
            const long fromBlockNumber = 13359463;
            const long toBlockNumber = 13359463 + 10;

            var mbs = await DB.ReadMevBlocks(fromBlockNumber, toBlockNumber);
            long i = toBlockNumber - 1;
            foreach (var b in mbs)
                Assert.AreEqual(b.BlockNumber, i--);
        }

        [TestMethod]
        public async Task BuildZmBlockJson()
        {
            const long blockNumber = 11207999;

            var json = await DB.GetZmBlockJson(blockNumber);
            Debug.WriteLine(json);
            var zb = JsonSerializer.Deserialize<ZMBlock>(json, ZMSerializeOptions.Default);
            Assert.AreEqual(zb.BlockNumber, blockNumber);
            Assert.AreEqual(zb.MevBlock.BlockNumber, blockNumber);
        }

        public static void DebugZMView(StreamWriter sw, long blockNumber)
        {
            var blocks = DB.ReadExtractorBlocks(blockNumber);
            ZMBlock zb = DB.BuildZMBlock(blocks);
            if (zb == null)
            {
                Console.WriteLine("skipped");
                return;
            }
            ZMView zv = new ZMView(blockNumber);
            zv.SetZMBlock(zb);

            sw.WriteLine($"{blockNumber},{zv.TxCount},{zv.BlockTimeAvg},{zv.BlockTimeRangeStdev.TotalSeconds},{(zv.TxCount == 0 ? string.Empty : zv.TxMeanStdev.TotalSeconds)}");

            for (int i = 0; i < zv.TxCount; i++)
            {
                ZMTx tx = zv.Txs[i];
                sw.WriteLine($"{tx.TxIndex},{tx.ArrivalMin},{tx.IsMiner}");
                for (int k = 0; k < zv.PoPs.Count; k++)
                {
                    if (zv.Txs[i].Arrivals[k] == null) continue;
                    TimeSpan ts = zv.Txs[i].Arrivals[k].ArrivalTime - zv.Txs[i].ArrivalMin;
                    if (ts.Ticks > 0)
                    {
                        //sw.WriteLine($"{zb.PoPs[k].Name},{(int)(ts.TotalSeconds)}");
                    }
                }
            }
        }

        public static void DebugBuildAPIBlock(long blockNumber)
        {
            var blocks = DB.ReadExtractorBlocks(blockNumber);
            ZMBlock ab = DB.BuildZMBlock(blocks);
            string json = "";
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 100000; i++)
                json = ab.ToString();
            sw.Stop();
            Console.WriteLine(json);
            Console.WriteLine($"{((double)sw.ElapsedMilliseconds) / 100000} ms");
        }

        public static void DebugReadExtractorBlocks(long blockNumber, int? extractorIndex = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var blocks = DB.ReadExtractorBlocks(blockNumber);
            sw.Stop();
            Console.WriteLine($"blocknum {blockNumber} in {sw.ElapsedMilliseconds} ms");
            foreach (var b in blocks)
            {
                if (extractorIndex.HasValue && b.ExtractorIndex != extractorIndex.Value)
                    continue;

                Console.WriteLine($"extractor {b.Extractor} block time {b.BlockTime}");
                if (b.TxTimes.Count == 0)
                {
                    Console.WriteLine("empty block");
                    continue;
                }
                int txIndex = 0;
                foreach (TxTime tx in b.TxTimes)
                    Console.WriteLine(Time.DurationStr(tx.ArrivalTime, b.BlockTime));
            }
            Console.WriteLine("");
        }
    }
}