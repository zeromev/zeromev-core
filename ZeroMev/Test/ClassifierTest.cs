using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Collections;
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
            const int toBlock = 13358564 + 1000;

            Tokens.Load();
            DEXs dexs = new DEXs();

            using (var db = new zeromevContext())
            {
                var swaps = from s in db.Swaps
                            where s.BlockNumber >= fromBlock && s.BlockNumber <= toBlock
                            orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                            select s;

                Stopwatch sw = Stopwatch.StartNew();
                int count = 0;
                foreach (var swap in swaps)
                    dexs.Add(swap, DateTime.Now.AddMinutes(count++));
                sw.Stop();
                double ms = (double)sw.ElapsedMilliseconds;
                Debug.WriteLine(sw.ElapsedMilliseconds + " ms");
                Debug.WriteLine((ms / (toBlock - fromBlock)) + " ms per block");
            }

            foreach (var dex in dexs.Values)
            {
                foreach (var pair in dex.Values)
                {
                    Debug.WriteLine($"{pair.ToString()} ${pair.LastXRate(Currency.USD)} {pair.LastXRate(Currency.ETH)} eth");
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