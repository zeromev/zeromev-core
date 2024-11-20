using Npgsql;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroMev.MevEFC;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.ClassifierService
{
    public class Utils
    {
        public static async Task ImportAllTokens()
        {
            // before running, populate zm_tokens table with unknown tokens using before_import_all_tokens.sql

            // get tokens with missing details
            List<ZmToken> missing;
            using (var db = new zeromevContext())
            {
                missing = (from t in db.ZmTokens
                           where t.Symbol == null
                           select t).ToList();
            }

            // and update them from the Ethplorer api
            HttpClient http = new HttpClient();
            foreach (var t in missing)
            {
                DateTime started = DateTime.Now;
                try
                {
                    var zt = await EthplorerAPI.GetTokenInfo(http, t.Address);
                    if (zt == null)
                    {
                        Debug.WriteLine($"{t.Address} null return");
                    }
                    else
                    {
                        // some symbols are messed up
                        if (zt.Symbol != null && zt.Name != null && zt.Symbol.Length > zt.Name.Length && zt.Name.Length > 2)
                            zt.Symbol = zt.Name;

                        // a new context each time to allow for recovery after connection failure
                        using (var db = new zeromevContext())
                        {
                            db.ZmTokens.Update(zt);
                            await db.SaveChangesAsync();
                        }
                        Debug.WriteLine($"{t.Address} {zt.Name} {zt.Symbol} {zt.Decimals}");
                    }
                }
                catch (HttpRequestException e)
                {
                    // probably doesn't exist- update the symbol to 'unknown' so we don't keep asking for it
                    ZmToken zt = new ZmToken();
                    zt.Address = t.Address;
                    zt.Symbol = Tokens.Unknown;
                    using (var db = new zeromevContext())
                    {
                        db.ZmTokens.Update(zt);
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{t.Address} errored: " + e.ToString());
                }

                // don't hammer the api
                TimeSpan duration = DateTime.Now - started;
                TimeSpan delay = new TimeSpan(0, 0, 0) - duration;
                if (delay.Ticks > 0)
                    await Task.Delay(delay);
            }
        }

        public static async Task ExportMevSummary()
        {
            const long first = API.EarliestMevBlock;
            const long last = 15683810;
            const long chunk = 1000;

            List<MEVBlock> queue = new List<MEVBlock>();
            for (long from = first; from <= last; from += chunk)
            {
                long to = from + chunk;
                if (to > last)
                    to = last;

                var mevBlocks = await DB.ReadMevBlocks(from, to);
                await DB.QueueWriteMevBlocksAsync(mevBlocks, Config.Settings.MevDB, queue, true);
            }
        }

        public static async void ExtractorCentralizationReport()
        {
            const string sql = "SELECT sandwiches.block_number, blocks.block_timestamp, sandwicher_address AS address, 'S' as mev_type FROM sandwiches, blocks WHERE sandwiches.block_number = blocks.block_number " +
            "UNION SELECT arbitrages.block_number, blocks.block_timestamp, account_address AS address, 'A' as mev_type FROM arbitrages, blocks WHERE arbitrages.block_number = blocks.block_number " +
            "UNION SELECT liquidations.block_number, blocks.block_timestamp, liquidator_user AS address, 'L' as mev_type FROM liquidations, blocks WHERE liquidations.block_number = blocks.block_number " +
            "ORDER BY block_number ASC;";

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.MevDB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Prepare();
                    var dr = await cmd.ExecuteReaderAsync();

                    ZMDecimal? nextBlockNumber = null;
                    while (dr.Read())
                    {
                        var block = (ZMDecimal)dr["block_number"];
                        if (nextBlockNumber == null || block > nextBlockNumber)
                        {
                            nextBlockNumber = block + 7200;
                        }
                    }
                }
                conn.Close();
            }
        }

        public async Task ContentCentralizationReport()
        {
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
                        var searcherId = tx.eoa_address.ToLower().Trim();
                        if (searchers.TryGetValue(searcherId, out searcherVolume))
                            searchers.Remove(searcherId);
                        searcherVolume += ZMDecimal.Parse(tx.total_miner_reward);
                        searchers.Add(searcherId, searcherVolume);
                    }
                }
            }
        }

        public async Task ImportAllFlashbots()
        {
            // this is a batch job to download, convert and import flashbots block data into zeromev

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

        public async Task ImportFillAfterAll()
        {
            // this is a batch job to fill in gaps after a full import of flashbots block data into zeromev

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
