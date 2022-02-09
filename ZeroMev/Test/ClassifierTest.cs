using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
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
        public async Task BuildDEXsTest()
        {
            DEXs dexs = new DEXs();

            using (var db = new zeromevContext())
            {
                var swaps = from s in db.Swaps
                            where s.BlockNumber >= 13358564 && s.BlockNumber <= 13359564
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
                        if (s.Order.BlockOrder.Blocknum == blockNumber)
                            sameBlockCount++;
                        else
                            blockNumber = s.Order.BlockOrder.Blocknum;
                    }
                    dexCount += pair.BlockOrder.Count;
                    dexSameBlockCount += sameBlockCount;
                    Console.WriteLine($"pair {pair.TokenA} {pair.TokenB} {sameBlockCount} / {pair.BlockOrder.Count} = {(double)sameBlockCount / (double)pair.BlockOrder.Count} USD {pair.LastXRate(Currency.USD)} ETH {pair.LastXRate(Currency.ETH)} BTC {pair.LastXRate(Currency.BTC)}");
                }
                Console.WriteLine($"dex {dexSameBlockCount} / {dexCount} = {(double)dexSameBlockCount / (double)dexCount}");

                allCount += dexCount;
                allSameBlockCount += dexSameBlockCount;
            }
            Console.WriteLine($"all {allSameBlockCount} / {allCount} = {(double)allSameBlockCount / (double)allCount}");
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
    }
}