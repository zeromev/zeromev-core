using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using ZeroMev.MevEFC;
using EFCore.BulkExtensions;
using System.Xml.Linq;

namespace ZeroMev.ClassifierService
{
    public class Classifier : BackgroundService
    {
        const int PollEverySecs = 5; // same assumptions as mev-inspect
        const int PollTimeoutSecs = 180;
        const int LogEvery = 100;
        const int GetNewTokensEverySecs = 240;
        const int ProcessChunkSize = 2000;
        const int ZmBlocksImportThreadCount = 24;
        const int ZmBlocksImportChunkSize = 256;

        readonly ILogger<Classifier> _logger;

        bool _isStopped = false;
        static HttpClient _http = new HttpClient();
        static DEXs _dexs = new DEXs();

        public Classifier(ILogger<Classifier> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // initialize
            Tokens.Load();
            DateTime classifierStartTime = DateTime.Now;
            DateTime lastProcessedBlockAt = DateTime.Now;
            DateTime lastGotTokens = DateTime.Now;

            // start with a zm_block import if specified
            await ZmBlockImportOptional(stoppingToken);
            if (_isStopped)
                return;

            try
            {
                // get latest tokens before any processing
                await UpdateNewTokens(_http);

                // startup and backfill
                bool started = await Startup(stoppingToken);
                if (!started)
                    return;

                // get the last recorded processed block
                long nextBlockNumber;
                using (var db = new zeromevContext())
                {
                    nextBlockNumber = db.GetLastZmProcessedBlock();
                }

                // start from one after the last processed
                nextBlockNumber++;
                _logger.LogInformation($"zm classifier starting at {classifierStartTime} from block {nextBlockNumber}");

                // classification loop- run until the service is stopped
                long miLastBlockNumber = 0;
                int delaySecs = 0;
                long count = 0;
                long? importToBlock = Config.Settings.ImportZmBlocksTo;

                while (!_isStopped)
                {
                    try
                    {
                        // optionally pause between cycles
                        await Task.Delay(TimeSpan.FromSeconds(delaySecs));
                        if (_isStopped)
                            break;

                        // default to poll time
                        delaySecs = PollEverySecs;

                        // mev-inspect uses a 5 block lag to ensure blocks are settled
                        // wait on mev-inspect, there is no point processing quicker than this as we need the mev data and the chain to be settled
                        if (nextBlockNumber > miLastBlockNumber && !importToBlock.HasValue)
                        {
                            using (var db = new zeromevContext())
                            {
                                miLastBlockNumber = db.GetLastProcessedMevInspectBlock();
                            }
                            if (nextBlockNumber > miLastBlockNumber)
                            {
                                _logger.LogInformation($"waiting {delaySecs} secs for mev-inspect {nextBlockNumber}");
                                continue;
                            }
                        }

                        // once we have those, get the block transaction count (by now considered trustworthy)
                        var r = await APIEnhanced.GetBlockWithTransactionStatus(_http, Num.LongToHex(nextBlockNumber));
                        if (r == null || r.TxStatus == null)
                        {
                            _logger.LogInformation($"waiting {delaySecs} secs for rpc txStatus {nextBlockNumber}");
                            continue;
                        }

                        // filter out invalid tx length extractor rows and calculate arrival times
                        var zb = DB.GetZMBlock(nextBlockNumber);
                        ZMView? zv = null;
                        if (zb != null)
                        {
                            zv = new ZMView(nextBlockNumber);
                            zv.RefreshOffline(zb, r.TxStatus.Count);
                        }

                        // if we don�t have enough zm blocks to process by now, wait up until the longer PollTimeoutSecs (it will likely mean the zeromevdb is down or something)
                        if (zb == null || zv == null)
                        {
                            // pause between polling if no block can be retrieved
                            // or after a longer timeout period, move on anyway so we don't get stuck
                            if (DateTime.Now < lastProcessedBlockAt.AddSeconds(PollTimeoutSecs))
                            {
                                _logger.LogInformation($"null zm block - polling {delaySecs} secs {nextBlockNumber}");
                                continue;
                            }

                            _logger.LogInformation($"null zm block - timeout {delaySecs} secs {nextBlockNumber}");
                        }

                        // classify mev 
                        using (var db = new zeromevContext())
                        {
                            if (zb != null && zv != null && zv.PoPs != null && zv.PoPs.Count != 0)
                            {
                                // write the count to the db (useful for later bulk reprocessing/restarts)
                                var txDataComp = Binary.Compress(Binary.WriteFirstSeenTxData(zv));
                                await db.AddZmBlock(nextBlockNumber, r.TxStatus.Count, zv.BlockTimeAvg, txDataComp, r.TxStatus, APIEnhanced.AddressesToBytes(r.Addresses));
                            }

                            var bp = BlockProcess.Load(nextBlockNumber, nextBlockNumber + 1, _dexs);
                            try
                            {
                                bp.Run();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"error processing {nextBlockNumber} {ex}");
                            }

                            try
                            {
                                await bp.Save(true);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"error saving {nextBlockNumber} {ex}");
                            }

                            try
                            {
                                await bp.SaveApi();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"error saving api {nextBlockNumber} {ex}");
                            }

                            await db.SetLastProcessedBlock(nextBlockNumber);
                            _logger.LogInformation($"processed {nextBlockNumber}");
                        }

                        // update tokens periodically (do within the cycle as Tokens are not threadsafe)
                        if (DateTime.Now > lastGotTokens.AddSeconds(GetNewTokensEverySecs))
                        {
                            _logger.LogInformation($"try get new tokens");
                            if (!await UpdateNewTokens(_http))
                                _logger.LogInformation($"get new tokens failed");
                            else
                                _logger.LogInformation($"got new tokens");

                            lastGotTokens = DateTime.Now;
                        }

                        // update progress
                        lastProcessedBlockAt = DateTime.Now;
                        nextBlockNumber++;

                        // don't pause if we need to catch up
                        if (nextBlockNumber < miLastBlockNumber || (importToBlock.HasValue && nextBlockNumber < importToBlock.Value))
                            delaySecs = 0;
                    }
                    catch (Exception ex)
                    {
                        // if this block has already written to the db, just skip it
                        if (ex != null && ex.InnerException != null && ex.InnerException.Message.Contains("duplicate key"))
                        {
                            _logger.LogInformation($"duplicate key {nextBlockNumber}, skipping");
                            nextBlockNumber++;
                            delaySecs = 0;
                            continue;
                        }

                        // an unexpected error likely means a database or network failure, and we must not progress onto a new block until this is rectified
                        _logger.LogInformation($"error block {nextBlockNumber}: {ex.ToString()}");
                        delaySecs = PollEverySecs;
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                // TODO log error and exit
                _logger.LogInformation($"error: " + e.ToString());
            }

            _logger.LogInformation($"zm classifier stopping (started {classifierStartTime})");
        }

        private async Task<bool> Startup(CancellationToken stoppingToken)
        {
            // process from 15 days before the last processed zeromev block up until the last processed mev-inspect block in manageable chunks
            // only save rows we haven't written before (ie: after the last zeromev block)

            long lastZmBlock;
            long? importFromBlock = Config.Settings.ImportZmBlocksFrom;
            if (importFromBlock == null)
            {
                using (var db = new zeromevContext())
                {
                    lastZmBlock = db.GetLastZmProcessedBlock();
                }
            }
            else
            {
                lastZmBlock = importFromBlock.Value;
            }

            long lastMiBlock;
            using (var db = new zeromevContext())
            {
                lastMiBlock = db.GetLastProcessedMevInspectBlock();
            }

            long first = lastZmBlock - Config.Settings.BlockBufferSize ?? 7200 * 15;
            long last = lastMiBlock + 1;
            long? importToBlock = Config.Settings.ImportZmBlocksTo;
            if (importToBlock != null && last > importToBlock.Value)
                last = importToBlock.Value;
            bool isBatchJob = (importFromBlock != null || importToBlock != null);

            try
            {
                for (long from = first; from <= last; from += ProcessChunkSize)
                {
                    long to = from + ProcessChunkSize;
                    if (to > last)
                        to = last;

                    double warmupProgress = (double)(from - first) / (lastZmBlock - first);
                    double totalProgress = (double)(from - first) / (last - first);
                    if (from < lastZmBlock)
                    {
                        _logger.LogInformation($"{from} to {to} (warmup to {lastZmBlock} {warmupProgress.ToString("P")}, backfill to {last} {totalProgress.ToString("P")})");
                    }
                    else
                    {
                        _logger.LogInformation($"{from} to {to} (backfill to {last} {totalProgress.ToString("P")})");
                    }


                    Stopwatch sw = new Stopwatch();
                    sw.Restart();
                    var bp = BlockProcess.Load(from, to, _dexs);
                    sw.Stop();
                    Console.WriteLine($"loaded in {sw.ElapsedMilliseconds} ms");

                    if (stoppingToken.IsCancellationRequested)
                        return false;

                    sw.Restart();
                    bp.Run();
                    if (stoppingToken.IsCancellationRequested)
                        return false;
                    sw.Stop();
                    Console.WriteLine($"run in {sw.ElapsedMilliseconds} ms");

                    await bp.Save(true, lastZmBlock);
                    await bp.SaveApi(lastZmBlock);
                    if (stoppingToken.IsCancellationRequested)
                        return false;

                    // set the last processed block (if not a batch job)
                    if (!isBatchJob)
                    {
                        using (var db = new zeromevContext())
                        {
                            await db.SetLastProcessedBlock(to - 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // important as network errors may make the RPC node or DB unavailable and we don't want gaps in the data
                _logger.LogError("startup aborted due to an unexpected error: " + ex.ToString());
                return false;
            }

            // if this is a batch job, signal to exit
            if (isBatchJob)
            {
                _logger.LogError($"exiting - import completed from {importFromBlock} to {importToBlock}");
                return false;
            }

            return true;
        }

        private async Task ZmBlockImportOptional(CancellationToken stoppingToken)
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "zm_blocks_import")
                    {
                        long first = long.Parse(args[i + 1]);
                        long last = long.Parse(args[i + 2]);
                        await ZmBlockImport(stoppingToken, first, last);
                    }
                }
            }
        }

        private async Task<bool> ZmBlockImport(CancellationToken stoppingToken, long first, long last)
        {
            List<ZmBlock> writeZmBlocks = new List<ZmBlock>();

            try
            {
                for (long from = first; from <= last; from += ZmBlocksImportChunkSize)
                {
                    if (_isStopped) break;

                    writeZmBlocks.Clear();

                    long to = from + ZmBlocksImportChunkSize;
                    if (to > last)
                        to = last;

                    double totalProgress = (double)(from - first) / (last - first);
                    _logger.LogInformation($"{from} to {to} (zm_block import from {first} to {last} {totalProgress.ToString("P")})");

                    // retrieve the next chunk of extractor blocks
                    var allebs = DB.ReadExtractorBlocks(from, to);

                    // iterate those in smaller chunks to split between threads
                    for (long bFirst = from; bFirst < to; bFirst += ZmBlocksImportThreadCount)
                    {
                        long bLast = bFirst + ZmBlocksImportThreadCount;
                        if (bLast > to)
                            bLast = to;

                        // parallelize rpc calls
                        var tasks = new Task<GetBlockWithTransactionStatusResult?>[ZmBlocksImportThreadCount];
                        int i = 0;
                        for (long b = bFirst; b < bLast; b++)
                            tasks[i++] = APIEnhanced.GetBlockWithTransactionStatus(_http, Num.LongToHex(b));

                        // combine results with extractor blocks
                        i = 0;
                        for (long b = bFirst; b < bLast; b++)
                        {
                            if (_isStopped) break;

                            var r = await tasks[i++];
                            if (r == null || r.TxStatus == null)
                                continue;

                            ZMBlock? zb = null;
                            ZMView? zv = null;
                            if (allebs.TryGetValue(b, out var ebs))
                            {
                                zb = DB.BuildZMBlock(ebs);
                                if (zb != null)
                                {
                                    zv = new ZMView(ebs[0].BlockNumber);
                                    zv.RefreshOffline(zb, r.TxStatus.Count);
                                }
                            }

                            DateTime? blockTime = null;
                            byte[]? txDataComp = null;
                            if (b >= API.EarliestZMBlock && zb != null && zv != null && zb.UniquePoPCount() != 0)
                            {
                                blockTime = zv.BlockTimeAvg;
                                txDataComp = Binary.Compress(Binary.WriteFirstSeenTxData(zv));
                            }
                            writeZmBlocks.Add(new ZmBlock() { BlockNumber = b, BlockTime = blockTime, TransactionCount = r.TxStatus.Count, TxData = txDataComp, TxStatus = r.TxStatus, TxAddresses = APIEnhanced.AddressesToBytes(r.Addresses) });
                        }
                    }

                    if (!_isStopped)
                    {
                        using (var db = new zeromevContext())
                        {
                            foreach (var block in writeZmBlocks)
                            {
                                await db.AddZmBlock(block.BlockNumber, block.TransactionCount, block.BlockTime, block.TxData, block.TxStatus, block.TxAddresses);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("ZmBlockImport aborted due to an unexpected error: " + ex.ToString());
            }
            return false;
        }

        public static async Task<bool> UpdateNewTokens(HttpClient http)
        {
            try
            {
                // get recently added tokens
                var tokens = await EthplorerAPI.GetTokensNew(http);
                if (tokens == null)
                    return false;

                // add any new tokens internally
                List<ZmToken> newtokens = new List<ZmToken>();
                foreach (var t in tokens)
                {
                    if (Tokens.Add(t))
                        newtokens.Add(t);
                }

                // then update the db with them
                if (newtokens.Count != 0)
                {
                    using (var db = new zeromevContext())
                    {
                        await db.BulkInsertOrUpdateAsync<ZmToken>(newtokens);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}