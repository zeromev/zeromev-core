using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroMev.ClassifierService;
using ZeroMev.MevEFC;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.Test
{
    [TestClass]
    public class ClassifierTest
    {
        [TestMethod]
        public async Task ReportSandwichMevProtection()
        {
            // retrieve mev in blocks of 1000
            // load zmview for each (backup db offline)
            // read sandwiches
            // output sandwiches: block_number,tx index + the below
            // sandwiched_count = total sandwiched transactions in this sandwich
            // sandwiched_miner_count = miner inserted but not in a bundle (possible sandwiching within other pools)
            // sandwiched_miner_bundles_count = miner inserted and in a bundle (detect sandwiching within flashbots)
            // and similarly for frontrun/backrun
            // note: a sandwich still counts as flashbots if only the frontrun is in a bundle, even if the backrun is not
            const long first = 13358564;
            const long last = 14153369;

            using (StreamWriter sw = new StreamWriter(@"E:\ReportSandwichMevProtection.txt"))
            {
                sw.WriteLine("block\ttxIndex\tsandwiched_count\tsandwiched_miner_count\tsandwiched_miner_bundles_count\tfrontrun_count\tfrontrun_miner_count\tfrontrun_miner_bundles_count\tbackrun_count\tbackrun_miner_count\tbackrun_miner_bundles_count");
                for (long b = first; b <= last; b++)
                {
                    try
                    {
                        var json = await DB.GetZmBlockJson(b);
                        var zb = JsonSerializer.Deserialize<ZMBlock>(json, ZMSerializeOptions.Default);
                        var zv = new ZMView(zb.BlockNumber);
                        if (zv.RefreshOffline(zb))
                        {
                            var mb = zb.MevBlock;
                            if (mb == null) continue;

                            for (int i = 0; i < mb.Sandwiched.Count; i++)
                            {
                                int sandwiched_count = 0;
                                int sandwiched_miner_count = 0;
                                int sandwiched_miner_bundles_count = 0;

                                int frontrun_count = 0;
                                int frontrun_miner_count = 0;
                                int frontrun_miner_bundles_count = 0;

                                int backrun_count = 0;
                                int backrun_miner_count = 0;
                                int backrun_miner_bundles_count = 0;

                                foreach (var s in mb.Sandwiched[i])
                                {
                                    if (s.TxIndex == null) continue;
                                    var ti = s.TxIndex.Value;
                                    sandwiched_count++;
                                    if (zv.Txs[ti].IsMiner)
                                    {
                                        sandwiched_miner_count++;
                                        if (zv.Txs[ti].FBBundle != null)
                                        {
                                            sandwiched_miner_bundles_count++;
                                        }
                                    }
                                }

                                var fi = mb.Frontruns[i].FrontrunSwapIndex;
                                if (fi != null)
                                {
                                    frontrun_count++;
                                    if (zv.Txs[fi.Value].IsMiner)
                                    {
                                        frontrun_miner_count++;
                                        if (zv.Txs[fi.Value].FBBundle != null)
                                        {
                                            frontrun_miner_bundles_count++;
                                        }
                                    }
                                }

                                var bi = mb.Backruns[i].BackrunSwapIndex;
                                if (bi != null)
                                {
                                    backrun_count++;
                                    if (zv.Txs[bi.Value].IsMiner)
                                    {
                                        backrun_miner_count++;
                                        if (zv.Txs[bi.Value].FBBundle != null)
                                        {
                                            backrun_miner_bundles_count++;
                                        }
                                    }
                                }

                                sw.WriteLine($"{b}\t{fi.Value}\t{sandwiched_count}\t{sandwiched_miner_count}\t{sandwiched_miner_bundles_count}\t{frontrun_count}\t{frontrun_miner_count}\t{frontrun_miner_bundles_count}\t{backrun_count}\t{backrun_miner_count}\t{backrun_miner_bundles_count}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestCalculateSandwiches()
        {
            const long testBlock = 13903978;

            var mevBlocks = await DB.ReadMevBlocks(testBlock, testBlock + 1);
            if (mevBlocks != null)
            {
                foreach (var mb in mevBlocks)
                {
                    var zv = new ZMView(mb.BlockNumber);
                    zv.RefreshOffline(null, 10000); // fake the tx count
                    zv.SetMev(mb);
                }
            }
        }

        [TestMethod]
        public void RunSandwichSim()
        {
            const int SandwichTxCount = 3; // profit will always be < victim impact for 3, victim impact can be > profit for 4+ as the model assumes sandwiched transactions are fair ordered (when usually they will not be)
            const int dec = 5;

            // verification that victim impact > sandwich profit
            ZMDecimal x = 10000;
            ZMDecimal y = 10000;
            ZMDecimal c = 0.997;

            Debug.WriteLine($"profitPercent\tvictimImpactPercent");
            ZMDecimal sumProfitPercent = 0;
            ZMDecimal sumVictimImpactPercent = 0;
            for (int i = 0; i < 10000; i++)
            {
                MEVHelper.SimUniswap2(SandwichTxCount, 0.5, false, true, c, x, y, out var a, out var b, out var xOut, out var yOut, out var isBA);

                // get the recalculated original set used for error reduction
                MEVCalc.CalculateSwaps(x, y, c, a, b, isBA, out var x_, out var y_, out var a_, out var b_, out var imbalanceSwitch);

                // sandwich profit / backrun victim impact
                ZMDecimal sandwichProfit;
                ZMDecimal? backrunVictimImpact = null;
                int backIndex = a.Length - 1;
                ZMDecimal[] af, bf;
                sandwichProfit = MEVCalc.SandwichProfitBackHeavy(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_, out backrunVictimImpact, out af, out bf);
                var profitPercent = (af[backIndex] / af[0]) - 1;

                // frontrun victim impact
                var bNoFrontrun = MEVCalc.FrontrunVictimImpact(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_);
                var victimImpact = b[0] - bNoFrontrun[0];
                var victimImpactPercent = (bNoFrontrun[1] / b[1]) - 1;
                sumProfitPercent += profitPercent;
                sumVictimImpactPercent += victimImpactPercent;
                Debug.WriteLine($"{profitPercent}\t{victimImpactPercent}");
            }
            Debug.WriteLine("-----\t-----");
            Debug.WriteLine($"{sumProfitPercent}\t{sumVictimImpactPercent}");
        }

        [TestMethod]
        public void TestProcessSandwiches()
        {
            var bp = BlockProcess.Load(11685998, 11687998, new DEXs());
            bp.Run();
        }

        [TestMethod]
        public void TestSimUniswapABAB()
        {
            MEVHelper.RunSimUniswap(false);
        }

        [TestMethod]
        public void TestSimUniswapABBA()
        {
            MEVHelper.RunSimUniswap(true);
        }

        [TestMethod]
        public async Task TestMevBlockSwaps()
        {
            const int dec = 5;
            var mevBlocks = await DB.ReadMevBlocks(13358565, 13358565 + 1000);

            foreach (var mb in mevBlocks)
            {
                if (mb.Frontruns.Count != mb.Backruns.Count || mb.Sandwiched.Count != mb.Frontruns.Count)
                    continue;

                for (int i = 0; i < mb.Frontruns.Count; i++)
                {
                    if (!MEVHelper.GetSandwichParametersFiltered(mb, i, out var real_a, out var real_b, ProtocolSwap.Unknown))
                        continue;

                    ZMDecimal c = 0.997; // 0.3% commission
                    ZMDecimal x, y;
                    MEVCalc.PoolFromSwapsABAB(real_a, real_b, c, out x, out y);

                    Debug.WriteLine($"block {mb.BlockNumber}");
                    Debug.WriteLine($"swap count {real_a.Length}");
                    if (real_a.Length > 3)
                        Debug.WriteLine(">3");
                    Debug.WriteLine($"x\t(ab)\t{x.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"y\t(ab)\t{y.RoundAwayFromZero(dec)}");
                    Debug.WriteLine("");

                    ZMDecimal[] a = new ZMDecimal[real_a.Length];
                    ZMDecimal[] b = new ZMDecimal[real_b.Length];
                    for (int j = 0; j < real_a.Length; j++)
                    {
                        if (j < real_a.Length - 1)
                        {
                            // ab swap
                            a[j] = real_a[j];
                            b[j] = MEVCalc.SwapOutputAmount(ref x, ref y, c, real_a[j]);
                        }
                        else
                        {
                            // ba swap
                            a[j] = MEVCalc.SwapOutputAmount(ref y, ref x, c, real_b[j]);
                            b[j] = real_b[j];
                        }
                    }

                    Debug.WriteLine("compare a");
                    Debug.WriteLine(Util.DisplayCompareAB("calculated", "real", a, real_a, dec));

                    Debug.WriteLine("compare b");
                    Debug.WriteLine(Util.DisplayCompareAB("calculated", "real", b, real_b, dec));
                    Debug.WriteLine("-----------------------");
                }
            }
        }

        [TestMethod]
        public async Task TestSandwichesExport()
        {
            const long first = 11207999;
            const long last = 14761425;
            const long chunk = 1000;

            using (StreamWriter sw = new StreamWriter(@"E:\TestSandwiches7FilteredWrapsAll.txt"))
            {
                sw.WriteLine("block\ttxIndex\tarbs\tfrontrunImpact\tbackrunImpact\tprofitPoolExtract\tprofitNaive\tprofitFrontrun\tprofitBackrun\tprofitRateDiff2Way\tprofitFbOnePercent");
                for (long from = first; from <= last; from += chunk)
                {
                    long to = from + chunk;
                    if (to > last)
                        to = last;

                    var mevBlocks = await DB.ReadMevBlocks(from, to);
                    foreach (var mb in mevBlocks)
                        MEVHelper.DebugMevBlock(mb, sw);
                }
            }
        }

        [TestMethod]
        public async Task TestMevBlockCalcs()
        {
            const int dec = 5;
            var mevBlocks = await DB.ReadMevBlocks(13587075 - 500, 13587075 + 500);

            foreach (var mb in mevBlocks)
            {
                if (mb.EthUsd > 4000) Debug.WriteLine($"{mb.BlockNumber},{mb.EthUsd}");
                var zv = new ZMView(mb.BlockNumber);
                zv.RefreshOffline(null, 10000); // fake the tx count
                zv.SetMev(mb);
            }
        }

        [TestMethod]
        public async Task BuildDEXsTest()
        {
            const int fromBlock = 13358564;
            const int toBlock = 13358564 + 10000;

            var dexs = new DEXs();
            var bi = BlockProcess.Load(fromBlock, toBlock, dexs);
            bi.Run();
            await bi.Save();
            Stopwatch sw = Stopwatch.StartNew();
            sw.Stop();

            double ms = (double)sw.ElapsedMilliseconds;
            Debug.WriteLine(sw.ElapsedMilliseconds + " ms");
            Debug.WriteLine((ms / (toBlock - fromBlock)) + " ms per block");
        }

        [TestMethod]
        public void TokensTest()
        {
            Tokens.Load();
            var usdc = Tokens.GetFromAddress("0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48");
            var dai = Tokens.GetFromAddress("0x6b175474e89094c44da98b954eedeac495271d0f");
            var usdt = Tokens.GetFromAddress("0xdac17f958d2ee523a2206206994597c13d831ec7");
            var error = Tokens.GetFromAddress("null error");

            Assert.AreEqual(usdc.Symbol, "USDC");
            Assert.AreEqual(dai.Symbol, "DAI");
            Assert.AreEqual(usdt.Symbol, "USDT");
            Assert.AreEqual(error, null);
        }

        [TestMethod]
        public async Task UpdateZmBlock()
        {
            using (var db = new zeromevContext())
            {
                await db.AddZmBlock(1, 0, new DateTime(2015, 7, 30), new byte[] { }, new BitArray(0));
            }

            using (var db = new zeromevContext())
            {
                await db.AddZmBlock(1, 0, new DateTime(2015, 7, 30), new byte[] { }, new BitArray(0));
            }
        }
    }
}