using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.Test
{
    [TestClass]
    public class FlashbotsAPITest
    {
        [TestMethod]
        public async Task GetFlashbotsRecent()
        {
            HttpClient http = new HttpClient();
            var r = await FlashbotsAPI.GetFlashbotsRecent(http);
        }

        [TestMethod]
        public async Task GetFlashbotsBlockByNumber()
        {
            HttpClient http = new HttpClient();
            long blockNumber = 13785855;
            var r = await FlashbotsAPI.GetFlashbotsBlockByNumber(http, blockNumber);
            Assert.AreEqual(r.blocks.Count, 1);
            Assert.AreEqual(r.blocks[0].block_number, blockNumber);
        }

        [TestMethod]
        public async Task ConvertToBitArray()
        {
            HttpClient http = new HttpClient();
            var r = await FlashbotsAPI.GetFlashbotsRecent(http);
            foreach (FBBlock fb in r.blocks)
            {
                BitArray ba = FlashbotsAPI.ConvertBundlesToBitArray(fb);
                string json = JsonSerializer.Serialize(ba, ZMSerializeOptions.Default);
                Debug.WriteLine(fb.block_number + " " + json);
            }
        }

        [TestMethod]
        public async Task DBWriteRead()
        {
            HttpClient http = new HttpClient();
            var r = await FlashbotsAPI.GetFlashbotsRecent(http);
            foreach (FBBlock fb in r.blocks)
            {
                BitArray ba = FlashbotsAPI.ConvertBundlesToBitArray(fb);
                DB.WriteFlashbotsBundles(fb);
                BitArray dbba = DB.ReadFlashbotsBundles(fb.block_number);

                Assert.AreEqual(dbba.Length, ba.Length);
                for (int i = 0; i < dbba.Length; i++)
                    Assert.AreEqual(dbba[i], ba[i]);
            }
        }

        [TestMethod]
        public async Task Collect()
        {
            HttpClient http = new HttpClient();
            await FlashbotsAPI.Collect(http, 100);
            await Task.Delay(5000);
        }
    }
}