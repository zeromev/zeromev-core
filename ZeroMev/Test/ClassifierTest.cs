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

namespace ZeroMev.Test
{
    [TestClass]
    public class ClassifierTest
    {
        [TestMethod]
        public void DebugSwapsByBlock()
        {
            using (var db = new zeromevContext())
            {
                var swaps = (from s in db.Swaps
                             where s.BlockNumber >= 13602897 && s.BlockNumber <= 13602897
                             orderby s.TraceAddress
                             select s).ToList();
                foreach (var s in swaps)
                    Debug.WriteLine(string.Join(",", s.TraceAddress));
            }
            return;

            const int fromBlock = 13602897 - 10000;
            const int toBlock = 13602897;

            var bi = BlockProcess.Load(fromBlock, toBlock);
            bi.DebugSwaps(13602897);
        }

        [TestMethod]
        public void BuildDEXsTest()
        {
            const int fromBlock = 13358564;
            const int toBlock = 13358564 + 500;

            var bi = BlockProcess.Load(fromBlock, toBlock);
            Stopwatch sw = Stopwatch.StartNew();
            bi.Process();
            sw.Stop();

            double ms = (double)sw.ElapsedMilliseconds;
            Debug.WriteLine(sw.ElapsedMilliseconds + " ms");
            Debug.WriteLine((ms / (toBlock - fromBlock)) + " ms per block");
            return;

            Tokens.Load();
            DEXs dexs = new DEXs();

            sw = Stopwatch.StartNew();
            List<Swap> swaps;
            using (var db = new zeromevContext())
            {
                swaps = (from s in db.Swaps
                         where s.BlockNumber >= fromBlock && s.BlockNumber <= toBlock
                         orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                         select s).ToList();
            }
            int count = 0;
            foreach (var swap in swaps)
            {
                var s = dexs.Add(swap, DateTime.Now.AddMinutes(count++), out var pair);
                /*
                ZMDecimal rateA = s.ARateUsd ??= new ZMDecimal();
                ZMDecimal usdA = s.AmountA * (s.ARateUsd ??= 1);
                ZMDecimal rateB = s.BRateUsd ??= new ZMDecimal();
                ZMDecimal usdB = s.AmountB * (s.BRateUsd ??= 1);
                Debug.WriteLine($"A {s.SymbolA} amount {s.AmountA} usd {usdA.RoundAwayFromZero(2)} rate {rateA.RoundAwayFromZero(5)}, B {s.SymbolB} amount {s.AmountB} usd {usdB.RoundAwayFromZero(2)} rate {rateB.RoundAwayFromZero(5)}");
                */
            }
            sw.Stop();

            ms = (double)sw.ElapsedMilliseconds;
            Debug.WriteLine(sw.ElapsedMilliseconds + " ms");
            Debug.WriteLine((ms / (toBlock - fromBlock)) + " ms per block");

            foreach (var dex in dexs.Values)
            {
                foreach (var pair in dex.Values)
                {
                    //Debug.WriteLine($"{pair.ToString()}");
                }
            }
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