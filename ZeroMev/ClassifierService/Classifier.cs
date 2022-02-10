using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using ZeroMev.MevEFC;

namespace ZeroMev.ClassifierService
{
    public class Classifier : BackgroundService
    {
        const int PollEverySecs = 5; // same assumptions as mev-inspect
        const int PollTimeoutSecs = 180;
        const bool DoBlockTxCountImport = true;

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
                        // there is no point processing quicker than this, as we need the mev data
                        // we must not progress past mev-inspect or we will miss data
                        // it is also good to use the same safety assumptions as mev-inspect

                        // so, wait on mev-inspect
                        if (nextBlockNumber > miLastBlockNumber && !DoBlockTxCountImport)
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

                        // once we have those, RPC the block tx count (by now considered trustworthy)
                        int? txCount = null;
                        txCount = await API.GetBlockTransactionCountByNumber(_http, nextBlockNumber);
                        if (!txCount.HasValue)
                        {
                            _logger.LogInformation($"waiting {delaySecs} secs for rpc tx count {nextBlockNumber}");
                            continue;
                        }

                        // write the count to the db (useful for later bulk reprocessing)
                        using (var db = new zeromevContext())
                        {
                            db.AddBlockTransactionCount(nextBlockNumber, txCount.Value);
                        }

                        // if we are only importing tx count, this is as far as we need to go
                        if (DoBlockTxCountImport)
                        {
                            // save work
                            if (nextBlockNumber % 10 == 0)
                            {
                                using (var db = new zeromevContext())
                                {
                                    await db.SetLastProcessedBlock(nextBlockNumber);
                                }
                            }
                            nextBlockNumber++;
                            delaySecs = 0; // import as fast as possible
                            continue;
                        }

                        // filter out invalid tx length extractor rows
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
                            // or after a longer timeout period, move on anyway
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
                                //_logger.LogInformation($"classified {nextBlockNumber} PoPs {zv.PoPs.Count}");
                            }

                            await db.SetLastProcessedBlock(nextBlockNumber);
                        }

                        // update progress
                        lastProcessedBlockAt = DateTime.Now;
                        nextBlockNumber++;

                        // don't pause if we need to catch up
                        if (nextBlockNumber < miLastBlockNumber)
                            delaySecs = 0;
                    }
                    catch (Exception ex)
                    {
                        // an unexpected error likely means a database or network failure, and we must not progress onto a new block until this is rectified
                        _logger.LogInformation($"error block {nextBlockNumber}: {ex.ToString()}");

                        // unless this block has already written to the db, in which case skip
                        if (ex != null && ex.InnerException != null && ex.InnerException.Message.Contains("duplicate key"))
                            nextBlockNumber++;

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