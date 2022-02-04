using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using ZeroMev.ClassifierService;
using ZeroMev.MevEFC;

namespace ZeroMev.Test
{
    [TestClass]
    public class ClassifierTest
    {
        [TestMethod]
        public async Task ClassifyTest()
        {
            using (var db = new zeromevContext())
            {
                await Classifier.ClassifyMEV(13377043, db);
            }
        }

        [TestMethod]
        public async Task BuildDEXsTest()
        {
            DEXs dexs = new DEXs();

            using (var db = new zeromevContext())
            {
                var swaps = from s in db.Swaps
                            where s.BlockNumber >= 13358564 && s.BlockNumber <= 13368564
                            orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                            select s;

                int mins = 0;
                foreach (var swap in swaps)
                    dexs.Add(swap, DateTime.Now.AddMinutes(mins++));
            }

            long allCount = 0;
            int allSameBlockCount = 0;
            foreach (var dex in dexs.Values)
            {
                Console.WriteLine(dex.AbiName + " " + dex.Protocol);

                long dexCount = 0;
                int dexSameBlockCount = 0;
                foreach (var pair in dex.Values)
                {
                    //if (pair.BlockOrder.Count < 100) continue;

                    long blockNumber = 0;
                    int sameBlockCount = 0;
                    foreach (var s in pair.BlockOrder.Values)
                    {
                        if (s.BlockOrder.Blocknum == blockNumber)
                            sameBlockCount++;
                        else
                            blockNumber = s.BlockOrder.Blocknum;
                    }
                    dexCount += pair.BlockOrder.Count;
                    dexSameBlockCount += sameBlockCount;
                    Console.WriteLine($"pair {pair.TokenA} {pair.TokenB} {sameBlockCount} / {pair.BlockOrder.Count} = {(double)sameBlockCount / (double)pair.BlockOrder.Count} rate: {pair.LastExchangeRate}");
                }
                Console.WriteLine($"dex {dexSameBlockCount} / {dexCount} = {(double)dexSameBlockCount / (double)dexCount}");

                allCount += dexCount;
                allSameBlockCount += dexSameBlockCount;
            }
            Console.WriteLine($"all {allSameBlockCount} / {allCount} = {(double)allSameBlockCount / (double)allCount}");
        }
    }
}