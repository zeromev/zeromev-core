using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Diagnostics;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.Test
{
    [TestClass]
    public class DBTest
    {
        public void TestExportBlocksToCsv()
        {
            using (StreamWriter sw = new StreamWriter(@"E:\block_vs_tx_stdev.csv"))
            {
                sw.WriteLine("BlockNumber,TxCount,BlockTimeAvg,BlockTimeStdevSecs,TxArrivalMeanStdevSecs");
                for (int bi = 13634387; bi >= 13358564; bi -= 200)
                {
                    DebugZMView(sw, bi);
                    //DB.DebugBuildAPIBlock(bi);
                    //DB.DebugReadExtractorBlocks(bi, 2);
                }
            }
            return;
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

            //Console.WriteLine(blockNumber + "," + zb.TxMeanStdev);
            //return;

            /*
            Console.WriteLine(blockNumber);
            foreach (PoP pop in zb.PoPs)
                Console.Write(pop.Name + ",");
            Console.WriteLine();

            foreach (PoP pop in zb.PoPs)
                Console.Write(pop.BlockTime + ",");
            Console.WriteLine();
            */

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
                    Console.WriteLine(Time.DurationStr(Time.ToUTC(b.Extractor, tx.ArrivalTime), b.BlockTime));
            }
            Console.WriteLine("");
        }
    }
}