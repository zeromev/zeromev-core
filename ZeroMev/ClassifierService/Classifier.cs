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

namespace ZeroMev.ClassifierService
{
    public class Classifier : BackgroundService
    {
        const int PollEverySecs = 5; // same assumptions as mev-inspect
        const int PollTimeoutSecs = 180;
        const int LogEvery = 100;
        const int GetNewTokensEverySecs = 240;

        readonly ILogger<Classifier> _logger;

        bool _isStopped = false;
        static HttpClient _http = new HttpClient();

        public Classifier(ILogger<Classifier> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // initialize
            DateTime classifierStartTime = DateTime.Now;
            DateTime lastProcessedBlockAt = DateTime.Now;
            DateTime lastGotTokens = DateTime.Now;

            try
            {
                // get the last recorded processed block
                long nextBlockNumber;
                using (var db = new zeromevContext())
                {
                    nextBlockNumber = db.GetLastProcessedBlock();
                }
                if (nextBlockNumber < 0)
                {
                    _logger.LogInformation($"zm classifier failed to get last processed block");
                    return;
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
                        int? txCount = null;
                        txCount = await API.GetBlockTransactionCountByNumber(_http, nextBlockNumber);
                        if (!txCount.HasValue)
                        {
                            _logger.LogInformation($"waiting {delaySecs} secs for rpc tx count {nextBlockNumber}");
                            continue;
                        }

                        // filter out invalid tx length extractor rows and calculate arrival times
                        var zb = DB.GetZMBlock(nextBlockNumber);
                        ZMView? zv = null;
                        if (zb != null)
                        {
                            zv = new ZMView(nextBlockNumber);
                            zv.RefreshOffline(zb, txCount.Value);
                        }

                        // if we don’t have enough zm blocks to process by now, wait up until the longer PollTimeoutSecs (it will likely mean the zeromevdb is down or something)
                        if (zb == null || zv == null || zb.UniquePoPCount() < 2)
                        {
                            string reason;
                            if (zb == null)
                                reason = "no zm block";
                            else
                                reason = zb.UniquePoPCount() + " pops";

                            // a successful attempt is at least 2 PoPs (extractors) providing data
                            // pause between polling if this criteria is not met
                            // or after a longer timeout period, move on anyway so we don't get stuck
                            if (DateTime.Now < lastProcessedBlockAt.AddSeconds(PollTimeoutSecs))
                            {
                                _logger.LogInformation($"polling {reason} {delaySecs} secs {nextBlockNumber}");
                                continue;
                            }

                            _logger.LogInformation($"timeout {reason} {delaySecs} secs {nextBlockNumber}");
                        }

                        // classify mev 
                        using (var db = new zeromevContext())
                        {
                            if (zb != null && zv != null && zv.PoPs != null && zv.PoPs.Count != 0)
                            {
                                // write the count to the db (useful for later bulk reprocessing/restarts)
                                var txDataComp = Binary.Compress(Binary.WriteFirstSeenTxData(zv));
                                await db.AddZmBlock(nextBlockNumber, txCount.Value, zv.BlockTimeAvg, txDataComp);

                                // paranoid integrity checks
                                var txData = Binary.Decompress(txDataComp);
                                var arrivals = Binary.ReadFirstSeenTxData(txData);
                                Debug.Assert(arrivals.Count == zv.Txs.Length);
                                for (int i = 0; i < arrivals.Count; i++)
                                    Debug.Assert(arrivals[i] == zv.Txs[i].ArrivalMin);
                            }

                            await db.SetLastProcessedBlock(nextBlockNumber);
                            if (count++ % LogEvery == 0)
                                _logger.LogInformation($"processed {nextBlockNumber} (log every {LogEvery})");
                        }

                        // update tokens periodically (do within the cycle as Tokens are not threadsafe)
                        if (DateTime.Now > lastGotTokens.AddSeconds(GetNewTokensEverySecs))
                        {
                            if (!await EthplorerAPI.UpdateNewTokens(_http))
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
    }
}