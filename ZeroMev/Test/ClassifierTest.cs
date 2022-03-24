using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.IO;
using System.Diagnostics;
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
            const long first = 13793168; //13358564;
            const long last = 13882400;
            const long chunk = 1000;

            using (StreamWriter sw = new StreamWriter(@"E:\TestSandwiches.csv"))
            {
                sw.WriteLine("block\ttxIndex\tXYMax\tfrontrunImpact\tbackrunImpact\tprofitPoolExtract\tprofitNaive\tprofitFrontrun\tprofitBackrun\tprofitRateDiff2Way\tprofitFbOnePercent");
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
            var mevBlocks = await DB.ReadMevBlocks(13389128 - 500, 13389128 + 500);

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
                await db.AddZmBlock(1, 0, new DateTime(2015, 7, 30), new byte[] { });
            }

            using (var db = new zeromevContext())
            {
                await db.AddZmBlock(1, 0, new DateTime(2015, 7, 30), new byte[] { });
            }
        }
    }
}