using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
        public void SimUniswap()
        {
            MEVHelper.RunSimUniswap();
        }

        [TestMethod]
        public async Task TestMevBlockCalcs()
        {
            const int dec = 5;
            var mevBlocks = await DB.ReadMevBlocks(13358565, 13358565 + 1000);

            foreach (var mb in mevBlocks)
            {
                if (mb.Frontruns.Count != mb.Backruns.Count || mb.Sandwiched.Count != mb.Frontruns.Count)
                    continue;

                for (int i = 0; i < mb.Frontruns.Count; i++)
                {
                    if (mb.BlockNumber == 13358968)
                        Console.Write("");

                    if (!MEVHelper.GetSandwichParameters(mb, i, out var real_a, out var real_b, ProtocolSwap.Uniswap2))
                        continue;

                    ZMDecimal c = 0.997; // 0.3% commission
                    ZMDecimal ab_x, ab_y, ab_k;
                    ZMDecimal ba_x, ba_y, ba_k;
                    MEVCalc.PoolFromSwapsAB(real_a, real_b, c, out ab_x, out ab_y, out ab_k);
                    MEVCalc.PoolFromSwapsBA(real_a, real_b, c, out ba_x, out ba_y, out ba_k);

                    Debug.WriteLine($"block {mb.BlockNumber}");
                    Debug.WriteLine($"swap count {real_a.Length}");
                    if (real_a.Length > 3)
                        Debug.WriteLine(">3");
                    Debug.WriteLine($"x\t(ab)\t{ab_x.RoundAwayFromZero(dec)}\t(ba)\t{ba_x.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"y\t(ab)\t{ab_y.RoundAwayFromZero(dec)}\t(ba)\t{ba_y.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"k\t(ab)\t{ab_k.RoundAwayFromZero(dec)}\t(ba)\t{ba_k.RoundAwayFromZero(dec)}");
                    Debug.WriteLine("");

                    ZMDecimal[] a = new ZMDecimal[real_a.Length];
                    ZMDecimal[] b = new ZMDecimal[real_b.Length];
                    for (int j = 0; j < real_a.Length; j++)
                    {
                        if (j < real_a.Length - 1)
                        {
                            // ab swap
                            a[j] = real_a[j];
                            b[j] = MEVHelper.SwapOutputAmount(ref ab_x, ref ab_y, c, real_a[j]);
                        }
                        else
                        {
                            // ba swap
                            a[j] = MEVHelper.SwapOutputAmount(ref ab_y, ref ab_x, c, real_b[j]);
                            b[j] = real_b[j];
                        }
                    }
                    /*
                    for (int j = 0; j < real_a.Length; j++)
                    {
                        if (j < real_a.Length - 1)
                        {
                            // ab swap
                            b[j] = MEVCalc.SwapAB(real_a[j], j == 0 ? 0 : b[j - 1], ab_x, ab_y, ab_k, c, out ab_x, out ab_y);

                            // maintain ba pool
                            a[j] = MEVCalc.SwapBA(b[j], j == 0 ? 0 : a[j - 1], ba_x, ba_y, ba_k, c, out ba_x, out ba_y);
                        }
                        else
                        {
                            // ba swap
                            a[j] = MEVCalc.SwapBA(real_b[j], j == 0 ? 0 : a[j - 1], ba_x, ba_y, ba_k, c, out ba_x, out ba_y);

                            // maintain ab pool
                            b[j] = MEVCalc.SwapAB(a[j], j == 0 ? 0 : b[j - 1], ab_x, ab_y, ab_k, c, out ab_x, out ab_y);
                        }
                    }
                    */

                    /*
                    Debug.WriteLine("calculated");
                    Debug.WriteLine(Util.DisplayArrayAB(a, b, dec));
                    Debug.WriteLine("real world");
                    Debug.WriteLine(Util.DisplayArrayAB(real_a, real_b, dec));
                    */

                    Debug.WriteLine("compare a");
                    Debug.WriteLine(Util.DisplayCompareAB("calculated", "real", a, real_a, dec));

                    Debug.WriteLine("compare b");
                    Debug.WriteLine(Util.DisplayCompareAB("calculated", "real", b, real_b, dec));

                    //if (a.Length > 3)
                    //if (mb.BlockNumber == 13358716)
                        //Console.WriteLine("whopper!");
                    Debug.WriteLine("-----------------------");
                }
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