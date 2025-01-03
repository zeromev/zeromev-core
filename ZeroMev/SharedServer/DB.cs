﻿using System;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Npgsql;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public class DB
    {
        const int MevCacheCacheExpirySecs = 3;

        // sql
        const string WriteExtractorBlockSQL = @"INSERT INTO public.extractor_block(" +
        "block_number, extractor_index, block_time, extractor_start_time, arrival_count, pending_count, tx_data) " +
        "VALUES (@block_number, @extractor_index, @block_time, @extractor_start_time, @arrival_count, @pending_count, @tx_data) " +
        "ON CONFLICT (block_number, extractor_index, block_time) DO UPDATE SET " +
        "extractor_start_time = EXCLUDED.extractor_start_time," +
        "arrival_count = EXCLUDED.arrival_count," +
        "pending_count = EXCLUDED.pending_count," +
        "tx_data = EXCLUDED.tx_data;";

        const string ReadExtractorBlockSQL = @"SELECT extractor_index, block_time, extractor_start_time, arrival_count, pending_count, tx_data " +
        "FROM public.extractor_block " +
        "WHERE block_number = @block_number " +
        "ORDER BY extractor_index asc, block_time desc;";

        const string ReadExtractorBlocksSQL = @"SELECT block_number, extractor_index, block_time, extractor_start_time, arrival_count, pending_count, tx_data " +
        "FROM public.extractor_block " +
        "WHERE block_number >= @from_block_number " +
        "AND block_number < @to_block_number " +
        "ORDER BY block_number asc, extractor_index asc, block_time desc;";

        const string ReadFlashbotsBundleSQL = @"SELECT bundle_data " +
        "FROM public.fb_block " +
        "WHERE block_number = @block_number;";

        const string ReadLatestMevBlockSQL = @"SELECT block_number FROM public.latest_mev_block LIMIT 1;";

        const string WriteLatestMevBlockSQL = @"UPDATE public.latest_mev_block SET block_number = @block_number WHERE @block_number > block_number;";

        const string ReadMevBlockSQL = @"SELECT mev_data " +
        "FROM public.mev_block " +
        "WHERE block_number = @block_number;";

        const string ReadMevBlocksSQL = @"SELECT mev_data " +
        "FROM public.mev_block " +
        "WHERE block_number >= @from_block_number AND block_number < @to_block_number " +
        "ORDER BY block_number DESC;";

        const string WriteFlashbotsBlockSQL = @"INSERT INTO public.fb_block(" +
        "block_number, bundle_data) " +
        "VALUES (@block_number, @bundle_data) ";

        const string WriteFlashbotsBlockSQLOnConflict = WriteFlashbotsBlockSQL + @"ON CONFLICT (block_number) DO UPDATE SET " +
        "bundle_data = EXCLUDED.bundle_data;";

        const string WriteMevBlockSQL = @"INSERT INTO public.mev_block(" +
        "block_number, mev_data) " +
        "VALUES (@block_number, @mev_data) ";

        const string WriteMevBlockSQLOnConflict = WriteMevBlockSQL + "ON CONFLICT (block_number) DO UPDATE SET " +
        "mev_data = EXCLUDED.mev_data;";

        const string WriteMevBlockSummarySQL = @"INSERT INTO public.zm_mev_summary(" +
        "block_number, mev_type, mev_amount_usd, mev_count) " +
        "VALUES (@block_number, @mev_type, @mev_amount_usd, @mev_count) ";

        const string WriteMevBlockSummarySQLOnConflict = WriteMevBlockSummarySQL + @"ON CONFLICT (block_number, mev_type) DO UPDATE SET " +
        "mev_amount_usd = EXCLUDED.mev_amount_usd, mev_count = EXCLUDED.mev_count;";

        const string WriteApiMevTxSQL = @"INSERT INTO public.zm_mev_transaction(" +
        "block_number, tx_index, mev_type, protocol, user_loss_usd, extractor_profit_usd, swap_volume_usd, swap_count, extractor_swap_volume_usd, extractor_swap_count, imbalance, address_from, address_to, arrival_time_us, arrival_time_eu, arrival_time_as) " +
        "VALUES (@block_number, @tx_index, @mev_type, @protocol, @user_loss_usd, @extractor_profit_usd, @swap_volume_usd, @swap_count, @extractor_swap_volume_usd, @extractor_swap_count, @imbalance, @address_from, @address_to, @arrival_time_us, @arrival_time_eu, @arrival_time_as) ";

        const string WriteApiMevTxSQLOnConflict = WriteApiMevTxSQL + "ON CONFLICT (block_number, tx_index, mev_type) DO UPDATE SET " +
        "protocol = EXCLUDED.protocol, user_loss_usd = EXCLUDED.user_loss_usd, extractor_profit_usd = EXCLUDED.extractor_profit_usd, swap_volume_usd = EXCLUDED.swap_volume_usd, swap_count = EXCLUDED.swap_count, extractor_swap_volume_usd = EXCLUDED.swap_volume_usd, extractor_swap_count = EXCLUDED.swap_count, imbalance = EXCLUDED.imbalance, address_from = EXCLUDED.address_from, address_to = EXCLUDED.address_to, arrival_time_us = EXCLUDED.arrival_time_us, arrival_time_eu = EXCLUDED.arrival_time_eu, arrival_time_as = EXCLUDED.arrival_time_as;";

        // write cache
        static List<ExtractorBlock> _extractorBlocks = new List<ExtractorBlock>();
        static List<FBBlock> _fbBlocks = new List<FBBlock>();

        static public long? LastBlockNumber = null;
        private static MEVLiteCache _mevCache = null;
        private static string? _mevCacheJson = JsonSerializer.Serialize<MEVLiteCache>(new MEVLiteCache(), ZMSerializeOptions.Default);
        private static DateTime _lastMevCache = DateTime.MinValue;
        private static object _mevCacheLock = new object();

        public static async Task<string> MEVLiteCacheJson(long lastBlockNumber)
        {
            // determine whether we need to refresh the cache in a threadsafe lock
            bool doGetNew = false;
            lock (_mevCacheLock)
            {
                var diff = DateTime.Now - _lastMevCache;
                if (diff.TotalSeconds > MevCacheCacheExpirySecs)
                {
                    _lastMevCache = DateTime.Now;
                    doGetNew = true;
                }
            }

            // refresh outside the lock
            if (doGetNew)
            {
                var cache = await RefreshMevCache();
                if (cache != null)
                {
                    _mevCache = cache;
                    _mevCacheJson = JsonSerializer.Serialize<MEVLiteCache>(cache, ZMSerializeOptions.Default);
                    _lastMevCache = DateTime.Now;
                }
            }

            // if the client already has the data, don't send again
            if (_mevCache != null && lastBlockNumber == _mevCache.LastBlockNumber)
                return "null";

            return _mevCacheJson;
        }

        private static async Task<MEVLiteCache> RefreshMevCache()
        {
            try
            {
                // if a new block has arrived, build and cache a new mev summary for the home page
                var latest = await DB.ReadLatestMevBlock();

                if (latest != null && DB.LastBlockNumber != latest)
                {
                    DB.LastBlockNumber = latest;

                    var mevBlocks = await DB.ReadMevBlocks(latest.Value - MEVLiteCache.CachedMevBlockCount, latest.Value);
                    if (mevBlocks != null)
                    {
                        MEVLiteCache cache = new MEVLiteCache();
                        cache.LastBlockNumber = latest;
                        bool isFirst = true;
                        foreach (var mb in mevBlocks)
                        {
                            var lb = new MEVLiteBlock(mb.BlockNumber, mb.BlockTime);
                            var zv = new ZMView(mb.BlockNumber);
                            zv.RefreshOffline(null, 10000); // fake the tx count
                            zv.SetMev(mb);
                            lb.MEVLite = BuildMevLite(zv);
                            if (lb.MEVLite.Count > 0 || isFirst)
                            {
                                isFirst = false;
                                cache.Blocks.Add(lb);
                                if (cache.Blocks.Count >= MEVLiteCache.CachedMevBlockCount) break;
                            }
                        }
                        return cache;
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public static List<MEVLite> BuildMevLite(ZMView zv)
        {
            var r = new List<MEVLite>();
            foreach (ZMTx tx in zv.Txs)
            {
                if (tx.MEV == null || tx.MEVClass == MEVClass.Info) continue;
                r.Add(new MEVLite(tx.MEV));
            }
            return r;
        }

        public static void QueueWriteExtractorBlockAsync(ExtractorBlock extractorBlock)
        {
            if (extractorBlock == null)
                return;

            // write any outstanding blocks and then the passed block async
            Task.Run(() =>
            {
                try
                {
                    lock (_extractorBlocks)
                    {
                        // employ a simple retry queue for writes so data is not lost even if the DB goes down for long periods (as long as the extractor keeps running)
                        _extractorBlocks.Add(extractorBlock);

                        while (_extractorBlocks.Count > 0)
                        {
                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            ExtractorBlock nextEb = _extractorBlocks[0];
                            WriteExtractorBlock(nextEb);
                            _extractorBlocks.RemoveAt(0);

                            sw.Stop();
                            Console.WriteLine($"extractor_block update {nextEb.TxTimes.Count} rows in {sw.ElapsedMilliseconds} ms {_extractorBlocks.Count} remaining");
                        }
                    }
                }
                catch (Exception e)
                {
                    // try catch is vital in this thread to avoid crashing the app
                    Console.WriteLine($"error QueueWriteExtractorBlockAsync {e.Message}, with {_extractorBlocks.Count} remaining");
                }

            }).ConfigureAwait(false);
        }

        public static void WriteExtractorBlock(ExtractorBlock eb)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();
                WriteExtractorBlock(eb, conn);
                conn.Close();
            }
        }

        public static void WriteExtractorBlock(ExtractorBlock eb, NpgsqlConnection conn)
        {
            byte[] txData = Binary.WriteTxData(eb.TxTimes);
            byte[] txDataComp = Binary.Compress(txData);

            using (var cmd = new NpgsqlCommand(WriteExtractorBlockSQL, conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter<long>("@block_number", eb.BlockNumber));
                cmd.Parameters.Add(new NpgsqlParameter<short>("@extractor_index", eb.ExtractorIndex));
                cmd.Parameters.Add(new NpgsqlParameter<DateTime>("@block_time", eb.BlockTime));
                cmd.Parameters.Add(new NpgsqlParameter<DateTime>("@extractor_start_time", eb.ExtractorStartTime));
                cmd.Parameters.Add(new NpgsqlParameter<long>("@arrival_count", eb.ArrivalCount));
                cmd.Parameters.Add(new NpgsqlParameter<int>("@pending_count", eb.PendingCount));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("@tx_data", txDataComp));

                cmd.Prepare();
                cmd.ExecuteNonQuery();
            }
        }

        public static List<ExtractorBlock> ReadExtractorBlocks(long blockNumber)
        {
            List<ExtractorBlock> ebs = null;

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ReadExtractorBlockSQL, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));
                    cmd.Prepare();
                    var dr = cmd.ExecuteReader();
                    ebs = ReadExtractorBlocks(blockNumber, dr);
                }
                conn.Close();
            }

            return ebs;
        }

        public static List<ExtractorBlock> ReadExtractorBlocks(long blockNumber, NpgsqlDataReader dr)
        {
            var ebs = new List<ExtractorBlock>();
            while (dr.Read())
            {
                //extractor_index, block_time, extractor_start_time, arrival_count, pending_count, tx_data
                ExtractorBlock eb = new ExtractorBlock(blockNumber,
                    (short)dr["extractor_index"],
                    DateTime.SpecifyKind((DateTime)dr["block_time"], DateTimeKind.Utc),
                    DateTime.SpecifyKind((DateTime)dr["extractor_start_time"], DateTimeKind.Utc),
                    (long)dr["arrival_count"],
                    (int)dr["pending_count"],
                    (byte[])dr["tx_data"]);
                ebs.Add(eb);
            }
            return ebs;
        }

        public static Dictionary<long, List<ExtractorBlock>> ReadExtractorBlocks(long fromBlockNumber, long toBlockNumber)
        {
            var all = new Dictionary<long, List<ExtractorBlock>>();

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ReadExtractorBlocksSQL, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@from_block_number", fromBlockNumber));
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@to_block_number", toBlockNumber));
                    cmd.Prepare();
                    var dr = cmd.ExecuteReader();

                    long currentBlockNumber = -1;
                    List<ExtractorBlock> ebs = null;
                    while (dr.Read())
                    {
                        //extractor_index, block_time, extractor_start_time, arrival_count, pending_count, tx_data
                        ExtractorBlock eb = new ExtractorBlock((long)dr["block_number"],
                            (short)dr["extractor_index"],
                            DateTime.SpecifyKind((DateTime)dr["block_time"], DateTimeKind.Utc),
                            DateTime.SpecifyKind((DateTime)dr["extractor_start_time"], DateTimeKind.Utc),
                            (long)dr["arrival_count"],
                            (int)dr["pending_count"],
                            (byte[])dr["tx_data"]);

                        if (currentBlockNumber != eb.BlockNumber)
                        {
                            if (ebs != null && ebs.Count != 0)
                                all.Add(ebs[0].BlockNumber, ebs);
                            ebs = new List<ExtractorBlock>();
                            currentBlockNumber = eb.BlockNumber;
                        }
                        ebs.Add(eb);
                    }

                    if (ebs != null && ebs.Count != 0)
                        all.Add(ebs[0].BlockNumber, ebs);
                }
                conn.Close();
            }

            return all;
        }

        public static void QueueWriteFlashbotsBlocksAsync(List<FBBlock> fbs)
        {
            if (fbs == null || fbs.Count == 0)
                return;

            // write any outstanding blocks and then the passed blocks async
            Task.Run(() =>
            {
                try
                {
                    lock (_fbBlocks)
                    {
                        // employ a simple retry queue for writes so data is not lost even if the DB goes down for long periods (as long as the extractor keeps running)
                        _fbBlocks.AddRange(fbs);

                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        while (_fbBlocks.Count > 0)
                        {
                            FBBlock nextFb = _fbBlocks[0];
                            WriteFlashbotsBundles(nextFb);
                            _fbBlocks.RemoveAt(0);
                        }

                        sw.Stop();
                        Console.WriteLine($"flashbots_block update in {sw.ElapsedMilliseconds} ms");
                    }
                }
                catch (Exception e)
                {
                    // try catch is vital in this thread to avoid crashing the app
                    Console.WriteLine($"error QueueWriteFlashbotsBlockAsync {e.Message}, with {_fbBlocks.Count} remaining");
                }

            }).ConfigureAwait(false);
        }

        public static void WriteFlashbotsBundles(FBBlock fb)
        {
            BitArray bundles = FlashbotsAPI.ConvertBundlesToBitArray(fb);
            if (bundles == null) return; // don't write missing/bad data

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();
                string sql = Config.Settings.FastImport ? WriteFlashbotsBlockSQL + ";" : WriteFlashbotsBlockSQLOnConflict;
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@block_number", fb.block_number));
                    cmd.Parameters.Add(new NpgsqlParameter<BitArray>("@bundle_data", bundles));

                    cmd.Prepare();
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
        }

        public static BitArray ReadFlashbotsBundles(long blockNumber)
        {
            BitArray bundle = null;

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ReadFlashbotsBundleSQL, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));
                    cmd.Prepare();
                    var dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        bundle = (BitArray)dr["bundle_data"];
                    }
                }
                conn.Close();
            }

            return bundle;
        }

        public static async Task QueueWriteApiTxsAsync(List<ApiMevTx> addApiTxs, string connectionStr, List<ApiMevTx> staticQueue)
        {
            try
            {
                // employ a simple retry queue for writes so data is not lost even if the DB goes down for long periods
                if (addApiTxs != null)
                    staticQueue.AddRange(addApiTxs);

                if (staticQueue.Count == 0)
                    return;

                Stopwatch sw = new Stopwatch();
                sw.Start();

                using (NpgsqlConnection conn = new NpgsqlConnection(connectionStr))
                {
                    conn.Open();

                    using (var batch = new NpgsqlBatch(conn))
                    {
                        foreach (var a in staticQueue)
                        {
                            string sql = Config.Settings.FastImport ? WriteApiMevTxSQL + ";" : WriteApiMevTxSQLOnConflict;
                            var cmdApiMevTx = new NpgsqlBatchCommand(sql);
                            cmdApiMevTx.Parameters.Add(new NpgsqlParameter<long>("@block_number", a.block_number));
                            cmdApiMevTx.Parameters.Add(new NpgsqlParameter<int>("@tx_index", a.tx_index));
                            cmdApiMevTx.Parameters.Add(new NpgsqlParameter<int>("@mev_type", (int)a.mev_type));
                            cmdApiMevTx.Parameters.Add(AddParam("@protocol", a.protocol));
                            cmdApiMevTx.Parameters.Add(AddParam("@user_loss_usd", a.user_loss_usd));
                            cmdApiMevTx.Parameters.Add(AddParam("@extractor_profit_usd", a.extractor_profit_usd));
                            cmdApiMevTx.Parameters.Add(AddParam("@swap_volume_usd", a.swap_volume_usd));
                            cmdApiMevTx.Parameters.Add(AddParam("@swap_count", a.swap_count));
                            cmdApiMevTx.Parameters.Add(AddParam("@extractor_swap_volume_usd", a.extractor_swap_volume_usd));
                            cmdApiMevTx.Parameters.Add(AddParam("@extractor_swap_count", a.extractor_swap_count));
                            cmdApiMevTx.Parameters.Add(AddParam("@imbalance", a.imbalance));
                            cmdApiMevTx.Parameters.Add(AddParam("@address_from", a.address_from));
                            cmdApiMevTx.Parameters.Add(AddParam("@address_to", a.address_to));
                            cmdApiMevTx.Parameters.Add(AddParam("arrival_time_us", a.arrival_time_us));
                            cmdApiMevTx.Parameters.Add(AddParam("arrival_time_eu", a.arrival_time_eu));
                            cmdApiMevTx.Parameters.Add(AddParam("arrival_time_as", a.arrival_time_as));
                            batch.BatchCommands.Add(cmdApiMevTx);
                        }
                        await batch.ExecuteNonQueryAsync();
                        staticQueue.Clear();
                    }

                    sw.Stop();
                    Console.WriteLine($"api mev txs update in {sw.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception e)
            {
                // try catch is vital in this thread to avoid crashing the app
                Console.WriteLine($"error QueueWriteApiTxsAsync {e.Message}, with {staticQueue.Count} remaining, db {connectionStr}");
            }
        }

        public static NpgsqlParameter AddParam(string columnName, object value)
        {
            if (value == null)
                return new NpgsqlParameter(columnName, DBNull.Value);
            return new NpgsqlParameter(columnName, value);
        }

        public static async Task QueueWriteMevBlocksAsync(List<MEVBlock> addMevBlocks, string connectionStr, List<MEVBlock> staticQueue, bool doWriteSummary)
        {
            if (addMevBlocks == null || addMevBlocks.Count == 0)
                return;

            try
            {
                // employ a simple retry queue for writes so data is not lost even if the DB goes down for long periods
                staticQueue.AddRange(addMevBlocks);

                Stopwatch sw = new Stopwatch();
                sw.Start();

                using (NpgsqlConnection conn = new NpgsqlConnection(connectionStr))
                {
                    conn.Open();

                    if (staticQueue.Count > 0)
                    {
                        var saveBlocks = new List<MEVBlock>(staticQueue);
                        await WriteMevBlock(conn, saveBlocks, doWriteSummary);
                        foreach (var mb in saveBlocks)
                            staticQueue.Remove(mb);
                    }
                }

                sw.Stop();
                Console.WriteLine($"mev blocks update in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                // try catch is vital in this thread to avoid crashing the app
                Console.WriteLine($"error QueueWriteMevBlocksAsync {e.Message}, with {staticQueue.Count} remaining, db {connectionStr}");
            }
        }

        public async static Task WriteMevBlock(NpgsqlConnection conn, List<MEVBlock> mbs, bool doWriteSummary)
        {
            long maxBlockNumber = 0;
            using (var batch = new NpgsqlBatch(conn))
            {
                foreach (MEVBlock mb in mbs)
                {
                    var mevJson = JsonSerializer.Serialize<MEVBlock>(mb, ZMSerializeOptions.Default);
                    var mevJsonComp = Binary.Compress(Encoding.ASCII.GetBytes(mevJson));

                    // mev block
                    string sql = Config.Settings.FastImport ? WriteMevBlockSQL + ";" : WriteMevBlockSQLOnConflict;
                    var cmdMevBlock = new NpgsqlBatchCommand(sql);
                    cmdMevBlock.Parameters.Add(new NpgsqlParameter<long>("@block_number", mb.BlockNumber));
                    cmdMevBlock.Parameters.Add(new NpgsqlParameter<byte[]>("@mev_data", mevJsonComp));
                    batch.BatchCommands.Add(cmdMevBlock);

                    if (mb.BlockNumber > maxBlockNumber) maxBlockNumber = mb.BlockNumber;
                }

                foreach (MEVBlock mb in mbs)
                {
                    // mev totals
                    var mev = MEVTypeSummaries.FromMevBlock(mb);

                    if (doWriteSummary)
                    {
                        foreach (var m in mev)
                        {
                            string sqlb = Config.Settings.FastImport ? WriteMevBlockSummarySQL + ";" : WriteMevBlockSummarySQLOnConflict;
                            var cmdMevBlockSummary = new NpgsqlBatchCommand(sqlb);
                            cmdMevBlockSummary.Parameters.Add(new NpgsqlParameter<long>("@block_number", mb.BlockNumber));
                            cmdMevBlockSummary.Parameters.Add(new NpgsqlParameter<int>("@mev_type", (int)m.MEVType));
                            cmdMevBlockSummary.Parameters.Add(new NpgsqlParameter<decimal>("@mev_amount_usd", m.AmountUsd));
                            cmdMevBlockSummary.Parameters.Add(new NpgsqlParameter<int>("@mev_count", m.Count));
                            batch.BatchCommands.Add(cmdMevBlockSummary);
                        }
                    }
                }

                // latest mev block
                var cmdLatestMevBlock = new NpgsqlBatchCommand(WriteLatestMevBlockSQL);
                cmdLatestMevBlock.Parameters.Add(new NpgsqlParameter<long>("@block_number", maxBlockNumber));
                batch.BatchCommands.Add(cmdLatestMevBlock);

                await batch.ExecuteNonQueryAsync();
            }
        }

        public static ZMBlock BuildZMBlock(List<ExtractorBlock> ebs)
        {
            if (ebs == null || ebs.Count == 0)
                return null;

            List<PoP> extractors = new List<PoP>();
            foreach (ExtractorBlock eb in ebs)
            {
                PoP ae = new PoP();
                ae.ExtractorIndex = eb.ExtractorIndex;
                ae.Name = eb.Extractor.ToString();
                ae.BlockTime = eb.BlockTime;
                ae.PendingCount = eb.PendingCount;
                ae.ExtractorStartTime = eb.ExtractorStartTime;
                ae.ArrivalCount = eb.ArrivalCount;
                ae.TxTimes = eb.TxTimes;
                extractors.Add(ae);
            }

            ZMBlock zb = new ZMBlock(ebs[0].BlockNumber, extractors);
            return zb;
        }

        public static ZMBlock GetZMBlock(long blockNumber)
        {
            var blocks = DB.ReadExtractorBlocks(blockNumber);
            return DB.BuildZMBlock(blocks);
        }

        public async static Task<string> GetZmBlockJson(long blockNumber)
        {
            // extractors
            var cmdExtractors = new NpgsqlBatchCommand(ReadExtractorBlockSQL);
            cmdExtractors.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));

            // flashbots
            var cmdFlashbots = new NpgsqlBatchCommand(ReadFlashbotsBundleSQL);
            cmdFlashbots.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));

            // mev
            byte[] mevComp = null;
            var connMev = new NpgsqlConnection(Config.Settings.MevWebDB);
            connMev.Open();
            var cmdMevBlock = new NpgsqlCommand(ReadMevBlockSQL, connMev);
            cmdMevBlock.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));
            var taskMev = cmdMevBlock.ExecuteReaderAsync();

            List<ExtractorBlock> ebs = null;
            BitArray bundles = null;

            using (var conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();

                // get them both in a single roundtrip
                await using var batch = new NpgsqlBatch(conn)
                {
                    BatchCommands =
                {
                    cmdExtractors,
                    cmdFlashbots
                }
                };
                await using var dr = await batch.ExecuteReaderAsync();

                // read extractors
                ebs = ReadExtractorBlocks(blockNumber, dr);

                // read flashbots
                dr.NextResult();
                while (dr.Read())
                    bundles = (BitArray)dr["bundle_data"];
            }

            // read mev
            using var drmev = await taskMev;
            while (drmev.Read())
                mevComp = (byte[])drmev["mev_data"];
            connMev.Close();

            // build zm block
            var zb = DB.BuildZMBlock(ebs);
            if (zb == null)
                zb = new ZMBlock(blockNumber, null);
            zb.Bundles = bundles;
            if (DB.LastBlockNumber != null)
                zb.LastBlockNumber = DB.LastBlockNumber;
            var json = JsonSerializer.Serialize(zb, ZMSerializeOptions.Default);

            // inject mev data into the json
            if (mevComp != null)
            {
                byte[] mev = Binary.Decompress(mevComp);
                var mevJson = ASCIIEncoding.ASCII.GetString(mev);
                json = json.Replace("\"mev\":null", "\"mev\":" + mevJson);
            }

            return json;
        }

        public async static Task<string> GetZmBlockMevJson(long blockNumber)
        {
            string json = "";

            // mev
            byte[] mevComp = null;
            var connMev = new NpgsqlConnection(Config.Settings.MevWebDB);
            connMev.Open();
            var cmdMevBlock = new NpgsqlCommand(ReadMevBlockSQL, connMev);
            cmdMevBlock.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));
            var taskMev = cmdMevBlock.ExecuteReaderAsync();

            // read mev
            using var drmev = await taskMev;
            while (drmev.Read())
                mevComp = (byte[])drmev["mev_data"];
            connMev.Close();

            // inject mev data into the json
            if (mevComp != null)
            {
                byte[] mev = Binary.Decompress(mevComp);
                json = ASCIIEncoding.ASCII.GetString(mev);
            }

            return json;
        }

        public static async Task<long?> ReadLatestMevBlock()
        {
            long? latestBlockNumber = null;

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.MevDB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ReadLatestMevBlockSQL, conn))
                {
                    cmd.Prepare();
                    var dr = await cmd.ExecuteReaderAsync();

                    while (dr.Read())
                        latestBlockNumber = (long)dr["block_number"];
                }
                conn.Close();
            }

            return latestBlockNumber;
        }

        public static async Task<List<MEVBlock>> ReadMevBlocks(long fromBlockNumber, long toBlockNumber)
        {
            List<MEVBlock> mbs = new List<MEVBlock>();

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.MevDB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ReadMevBlocksSQL, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@from_block_number", fromBlockNumber));
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@to_block_number", toBlockNumber));
                    cmd.Prepare();

                    var dr = await cmd.ExecuteReaderAsync();
                    while (dr.Read())
                    {
                        byte[] dataComp = (byte[])dr["mev_data"];
                        var data = Binary.Decompress(dataComp);
                        var json = ASCIIEncoding.ASCII.GetString(data);
                        var mb = JsonSerializer.Deserialize<MEVBlock>(json, ZMSerializeOptions.Default);
                        mbs.Add(mb);
                    }
                }
                conn.Close();
            }

            return mbs;
        }
    }
}