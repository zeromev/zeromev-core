using System;
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

        const string WriteFlashbotsBlockSQL = @"INSERT INTO public.fb_block(" +
        "block_number, bundle_data) " +
        "VALUES (@block_number, @bundle_data) " +
        "ON CONFLICT (block_number) DO UPDATE SET " +
        "bundle_data = EXCLUDED.bundle_data;";

        const string ReadFlashbotsBundleSQL = @"SELECT bundle_data " +
        "FROM public.fb_block " +
        "WHERE block_number = @block_number;";

        const string WriteMevBlockSQL = @"INSERT INTO public.mev_block(" +
        "block_number, mev_data) " +
        "VALUES (@block_number, @mev_data) " +
        "ON CONFLICT (block_number) DO UPDATE SET " +
        "mev_data = EXCLUDED.mev_data;";

        const string ReadMevBlockSQL = @"SELECT mev_data " +
        "FROM public.mev_block " +
        "WHERE block_number = @block_number;";

        // write cache
        static List<ExtractorBlock> _extractorBlocks = new List<ExtractorBlock>();
        static List<FBBlock> _fbBlocks = new List<FBBlock>();
        static List<MEVBlock2> _mevBlocks = new List<MEVBlock2>();

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
            byte[] txData = Binary.WriteTxData(eb.TxTimes);
            byte[] txDataComp = Binary.Compress(txData);

            using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(WriteExtractorBlockSQL, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@block_number", eb.BlockNumber));
                    cmd.Parameters.Add(new NpgsqlParameter<short>("@extractor_index", eb.ExtractorIndex));
                    cmd.Parameters.Add(new NpgsqlParameter<DateTime>("@block_time", eb.BlockTime.ToUniversalTime()));
                    cmd.Parameters.Add(new NpgsqlParameter<DateTime>("@extractor_start_time", eb.ExtractorStartTime.ToUniversalTime()));
                    cmd.Parameters.Add(new NpgsqlParameter<long>("@arrival_count", eb.ArrivalCount));
                    cmd.Parameters.Add(new NpgsqlParameter<int>("@pending_count", eb.PendingCount));
                    cmd.Parameters.Add(new NpgsqlParameter<byte[]>("@tx_data", txDataComp));

                    cmd.Prepare();
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
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
                    ((DateTime)dr["block_time"]).ToUniversalTime(),
                    ((DateTime)dr["extractor_start_time"]).ToUniversalTime(),
                    (long)dr["arrival_count"],
                    (int)dr["pending_count"],
                    (byte[])dr["tx_data"]);
                ebs.Add(eb);
            }
            return ebs;
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
                using (var cmd = new NpgsqlCommand(WriteFlashbotsBlockSQL, conn))
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

        public static void QueueWriteMevBlocksAsync(List<MEVBlock2> mbs)
        {
            if (mbs == null || mbs.Count == 0)
                return;

            try
            {
                lock (_mevBlocks)
                {
                    // employ a simple retry queue for writes so data is not lost even if the DB goes down for long periods (as long as the extractor keeps running)
                    _mevBlocks.AddRange(mbs);

                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    using (NpgsqlConnection conn = new NpgsqlConnection(Config.Settings.DB))
                    {
                        conn.Open();

                        while (_mevBlocks.Count > 0)
                        {
                            MEVBlock2 nextMevBlock = _mevBlocks[0];
                            WriteMevBlock(conn, nextMevBlock);
                            _mevBlocks.RemoveAt(0);
                        }
                    }

                    sw.Stop();
                    Console.WriteLine($"flashbots_block update in {sw.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception e)
            {
                // try catch is vital in this thread to avoid crashing the app
                Console.WriteLine($"error QueueWriteFlashbotsBlockAsync {e.Message}, with {_mevBlocks.Count} remaining");
            }
        }

        public static void WriteMevBlock(NpgsqlConnection conn, MEVBlock2 mb)
        {
            var mevJson = JsonSerializer.Serialize<MEVBlock2>(mb, ZMSerializeOptions.Default);
            var mevJsonComp = Binary.Compress(Encoding.ASCII.GetBytes(mevJson));

            using (var cmd = new NpgsqlCommand(WriteMevBlockSQL, conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter<long>("@block_number", mb.BlockNumber));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("@mev_data", mevJsonComp));

                cmd.Prepare();
                cmd.ExecuteNonQuery();
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
            var cmdMevBlock = new NpgsqlBatchCommand(ReadMevBlockSQL);
            cmdMevBlock.Parameters.Add(new NpgsqlParameter<long>("@block_number", blockNumber));

            List<ExtractorBlock> ebs = null;
            BitArray bundles = null;
            byte[] mevComp = null;

            using (var conn = new NpgsqlConnection(Config.Settings.DB))
            {
                conn.Open();

                // get them all in a single roundtrip
                await using var batch = new NpgsqlBatch(conn)
                {
                    BatchCommands =
                {
                    cmdExtractors,
                    cmdFlashbots,
                    cmdMevBlock
                }
                };
                await using var dr = await batch.ExecuteReaderAsync();

                // read extractors
                ebs = ReadExtractorBlocks(blockNumber, dr);

                // read flashbots
                dr.NextResult();
                while (dr.Read())
                    bundles = (BitArray)dr["bundle_data"];

                // read mev
                dr.NextResult();
                while (dr.Read())
                    mevComp = (byte[])dr["mev_data"];
            }

            // build zm block
            var zb = DB.BuildZMBlock(ebs);
            if (zb == null)
                zb = new ZMBlock(blockNumber, null);
            zb.Bundles = bundles;
            var json = JsonSerializer.Serialize(zb, ZMSerializeOptions.Default);

            // inject mev data into the json
            if (mevComp != null)
            {
                byte[] mev = Binary.Decompress(mevComp);
                var mevJson = ASCIIEncoding.ASCII.GetString(mev);
                json = json.Replace("\"mevBlock\":null", "\"mevBlock\":" + mevJson);
            }

            return json;
        }
    }
}