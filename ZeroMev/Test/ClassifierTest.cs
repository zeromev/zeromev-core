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
        public void JsonBigInt()
        {
            MEVLiquidation l = new MEVLiquidation(1, "hash", ProtocolLiquidation.Aave, 10, 10, 0, 10, 10, 2, false, null, null);
            Debug.WriteLine(JsonSerializer.Serialize(l, ZMSerializeOptions.Default));

            MEVSwap s = new MEVSwap(new TraceAddress(new int[] { }), ProtocolSwap.Uniswap2, 10, 11, 0, 1, 0, 1, null, null);
            Debug.WriteLine(JsonSerializer.Serialize(s, ZMSerializeOptions.Default));

            Debug.WriteLine("BigInteger");
            Debug.WriteLine(JsonSerializer.Serialize((BigInteger)0, ZMSerializeOptions.Default));
            Debug.WriteLine(JsonSerializer.Serialize((BigInteger)1, ZMSerializeOptions.Default));
            Debug.WriteLine(JsonSerializer.Serialize((BigInteger)2, ZMSerializeOptions.Default));
            Debug.WriteLine(JsonSerializer.Serialize((BigInteger)232432, ZMSerializeOptions.Default));

            Debug.WriteLine("ZMDecimal");
            Debug.WriteLine(JsonSerializer.Serialize((ZMDecimal?)0, ZMSerializeOptions.Default));
            Debug.WriteLine(JsonSerializer.Serialize((ZMDecimal?)1, ZMSerializeOptions.Default));
            Debug.WriteLine(JsonSerializer.Serialize((ZMDecimal?)2, ZMSerializeOptions.Default));
            Debug.WriteLine(JsonSerializer.Serialize((ZMDecimal?)232432, ZMSerializeOptions.Default));
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
                await db.AddZmBlock(1, 0, new DateTime(2015, 7, 30), new byte[] { }, new BitArray(0), new byte[] { });
            }

            using (var db = new zeromevContext())
            {
                await db.AddZmBlock(1, 0, new DateTime(2015, 7, 30), new byte[] { }, new BitArray(0), new byte[] { });
            }
        }
    }
}