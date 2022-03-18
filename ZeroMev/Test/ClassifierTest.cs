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
        public void TestKPoolSolver()
        {
            // from sample block 13358716
            ZMDecimal x_orig = 41.121360656352509821828771208003135264064987170884;
            ZMDecimal y_orig = 885843.45906323353841265322065768882728693088827573;
            ZMDecimal k_orig = x_orig * y_orig;  // real = 36426634.104000091604669404689204579118861600150817
            ZMDecimal[] real_a = new ZMDecimal[] { 5.258123867872190644, 0.315485239562152251, 0.15, 5.329607408038048539 }; // from the chain
            ZMDecimal[] real_b = new ZMDecimal[] { 100741.652958029841399841, 5322.392691288202685744, 2505.428899786084903151, 100741.65295802984139984 }; // from the chain
            bool[] isBuy = new bool[] { true, true, true, false };
            ZMDecimal c = 0.997; // 0.3% commission
            const int dec = 5;

            var x = x_orig;
            var y = y_orig;
            var k = k_orig;
            ZMDecimal[] a = new ZMDecimal[real_a.Length];
            ZMDecimal[] b = new ZMDecimal[real_a.Length];
            for (int j = 0; j < real_a.Length; j++)
            {
                if (isBuy[j])
                {
                    a[j] = real_a[j];
                    b[j] = MEVCalc.SwapAForB(real_a[j], x, y, k, c, out x, out y);
                }
                else
                {
                    a[j] = MEVCalc.SwapBForA(real_b[j], x, y, k, c, out x, out y);
                    b[j] = real_b[j];
                }
            }

            MEVCalc.KPoolFromSwaps(a, b, c, out var x_, out var y_, out var k_);
            Debug.WriteLine($"x {x_orig.RoundAwayFromZero(dec)} x_ {x_.RoundAwayFromZero(dec)}");
            Debug.WriteLine($"y {y_orig.RoundAwayFromZero(dec)} y_ {y_.RoundAwayFromZero(dec)}");
            Debug.WriteLine($"k {k_orig.RoundAwayFromZero(dec)} k_ {k_.RoundAwayFromZero(dec)}");
            Debug.WriteLine("");

            ZMDecimal[] a_ = new ZMDecimal[a.Length];
            ZMDecimal[] b_ = new ZMDecimal[b.Length];
            for (int j = 0; j < real_a.Length; j++)
            {
                if (isBuy[j])
                    b_[j] = MEVCalc.SwapAForB(real_a[j], x_, y_, k_, c, out x_, out y_);
                else
                    a_[j] = MEVCalc.SwapBForA(real_b[j], x_, y_, k_, c, out x_, out y_);
            }

            Debug.WriteLine("known");
            Debug.WriteLine(Util.DisplayArrayAB(a, b, dec));
            Debug.WriteLine("calculated");
            Debug.WriteLine(Util.DisplayArrayAB(a_, b_, dec));
            Debug.WriteLine("real world");
            Debug.WriteLine(Util.DisplayArrayAB(real_a, real_b, dec));
            Debug.WriteLine("-----------------------");
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
                    if (mb.BlockNumber == 13358965 || mb.BlockNumber == 13358716 || mb.BlockNumber == 13358968)
                        Console.Write("");

                    if (!MEVHelper.GetSandwichParameters(mb, i, out var real_a, out var real_b, ProtocolSwap.Uniswap2))
                        continue;

                    ZMDecimal c = 0.997; // 0.3% commission
                    ZMDecimal x, y, k;
                    MEVCalc.KPoolFromSwaps(real_a, real_b, c, out x, out y, out k);

                    Debug.WriteLine($"block {mb.BlockNumber}");
                    Debug.WriteLine($"swap count {real_a.Length}");
                    if (real_a.Length > 3)
                        Debug.WriteLine(">3");
                    Debug.WriteLine($"x {x.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"y {y.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"k {k.RoundAwayFromZero(dec)}");
                    Debug.WriteLine("");

                    /*
                    MEVCalc.KPoolFromSwaps(real_b, real_a, c, out var xR, out var yR, out var kR);
                    Debug.WriteLine($"xR {xR.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"yR {yR.RoundAwayFromZero(dec)}");
                    Debug.WriteLine($"kR {kR.RoundAwayFromZero(dec)}");
                    Debug.WriteLine("");
                    */

                    ZMDecimal[] a_ = new ZMDecimal[real_a.Length];
                    ZMDecimal[] b_ = new ZMDecimal[real_b.Length];
                    for (int j = 0; j < real_a.Length; j++)
                    {
                        a_[j] = real_a[j];
                        b_[j] = MEVCalc.SwapAForB(real_a[j], x, y, k, c, out x, out y);
                    }

                    Debug.WriteLine("calculated");
                    Debug.WriteLine(Util.DisplayArrayAB(a_, b_, dec));
                    Debug.WriteLine("real world");
                    Debug.WriteLine(Util.DisplayArrayAB(real_a, real_b, dec));

                    Debug.WriteLine("compare a: calculated vs real");
                    Debug.WriteLine(Util.DisplayCompareAB(a_, real_a, dec));

                    Debug.WriteLine("compare b: calculated vs real");
                    Debug.WriteLine(Util.DisplayCompareAB(b_, real_b, dec));

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