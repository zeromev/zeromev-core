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

        [TestMethod]
        public async Task ImportAll()
        {
            // not a test
            // this is a batch job to download, convert and import flashbots block data into zeromev
            return;

            const string filename = @"E:\FlashbotsAllBlocks.json";
            await ImportFlashbotsAllJson(filename);

            using (StreamReader sr = new StreamReader(File.OpenRead(filename)))
            {
                var fbs = await JsonSerializer.DeserializeAsync<List<FBBlock?>>(sr.BaseStream, ZMSerializeOptions.Default);

                // write to local db
                // then export and import table
                foreach (FBBlock fb in fbs)
                {
                    BitArray ba = FlashbotsAPI.ConvertBundlesToBitArray(fb);
                    DB.WriteFlashbotsBundles(fb);
                }
            }
        }

        [TestMethod]
        public async Task ContentCentralizationReport()
        {
            // not a test
            //return;

            // on a daily basis, how many searchers account for the top percentage of bundles
            // maintain a sorted list of searchers (prob eoa_address, poss to_address)
            // at the end of each day, iterate list in order of size desc, counting the number of unique searchers until the threshold is reached
            // record that number

            const int BlocksPerDay = 7200;
            ZMDecimal topPercent = 0.95;
            const string fbJsonFilename = @"E:\FlashbotsAllBlocks.json";
            const string outputFilename = @"E:\ContentCentralizationReport.txt";
            await ImportFlashbotsAllJson(fbJsonFilename);

            Dictionary<string, ZMDecimal> searchers = new Dictionary<string, ZMDecimal>();
            using (StreamWriter sw = new StreamWriter(outputFilename))
            using (StreamReader sr = new StreamReader(File.OpenRead(fbJsonFilename)))
            {
                sw.WriteLine($"to_block_number,top_searcher_count,searcher_count,top_searcher_pct");

                var fbs = await JsonSerializer.DeserializeAsync<List<FBBlock?>>(sr.BaseStream, ZMSerializeOptions.Default);

                // write to local db
                // then export and import table
                long nextBlock = fbs[fbs.Count - 1].block_number + BlocksPerDay;
                for (int b = fbs.Count - 1; b >= 0; b--)
                {
                    FBBlock fb = fbs[b];

                    // calculate top searchers daily (approximately)
                    if (fb.block_number > nextBlock)
                    {
                        // determine top searcher count
                        ZMDecimal[] vols = new ZMDecimal[searchers.Count];
                        searchers.Values.CopyTo(vols, 0);
                        Array.Sort(vols);

                        // determine top searcher threshold
                        ZMDecimal sum = 0;
                        for (int i = vols.Length - 1; i >= 0; i--)
                            sum += vols[i];
                        var threshold = sum * topPercent;

                        // and count up to it
                        int topCount = 0;
                        sum = 0;
                        for (int i = vols.Length - 1; i >= 0; i--)
                        {
                            sum += vols[i];
                            topCount++;
                            if (sum > threshold)
                                break;
                        }
                        var pct = ((double)topCount) / vols.Length;
                        sw.WriteLine($"{fb.block_number - 1},{topCount},{vols.Length},{pct}");

                        // reset for the next day
                        searchers.Clear();
                        nextBlock = fb.block_number + BlocksPerDay;
                    }

                    // maintain daily volumes for each searcher
                    foreach (var tx in fb.transactions)
                    {
                        ZMDecimal searcherVolume = 0;
                        var searcherId = tx.eoa_address;
                        if (searchers.TryGetValue(searcherId, out searcherVolume))
                            searchers.Remove(searcherId);
                        searcherVolume += ZMDecimal.Parse(tx.total_miner_reward);
                        searchers.Add(searcherId, searcherVolume);
                    }
                }
            }
        }

        private async Task ImportFlashbotsAllJson(string filename)
        {
            // create or reuse existing all blocks cache
            if (!File.Exists(filename))
            {
                HttpClient http = new HttpClient();
                using (HttpResponseMessage response = await http.GetAsync(FlashbotsAPI.UrlFlashbotsAllBlocks, HttpCompletionOption.ResponseHeadersRead))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    using (Stream streamToWriteTo = File.Open(filename, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                    }
                }
            }
        }

        [TestMethod]
        public async Task ImportFillAfterAll()
        {
            // not a test
            // this is a batch job to fill in gaps after a full import of flashbots block data into zeromev
            return;

            long fromBlock = 13990881;
            long toBlock = 13991015;

            HttpClient http = new HttpClient();
            for (long b = 13991015; b >= 13990881; b--)
            {
                var r = await FlashbotsAPI.GetFlashbotsBlockByNumber(http, b);
                if (r.blocks != null && r.blocks.Count > 0)
                    DB.WriteFlashbotsBundles(r.blocks[0]);
                await Task.Delay(2000); // don't hammer the server
            }
        }
    }
}