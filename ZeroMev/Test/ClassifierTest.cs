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