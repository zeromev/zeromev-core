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

        // write cache
        static List<ExtractorBlock> _extractorBlocks = new List<ExtractorBlock>();
        static List<FBBlock> _fbBlocks = new List<FBBlock>();

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

                    ebs = new List<ExtractorBlock>();
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
                }
                conn.Close();
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
    }
}