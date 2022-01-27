using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using ZeroMev.MevEFC;

namespace ZeroMev.ClassifierService
{
    public class Classifier
    {
        const int PollEverySecs = 6;
        const int PollTimeoutSecs = 180;
        const int MinimumTimeSinceLastBlockTimestampSecs = 24; // give our extractors time to return data

        ILogger _logger;
        bool _isStopped = false;
        DateTime _classifierStartTime;

        static HttpClient _http = new HttpClient();

        public bool HadConnectionException { get; private set; }

        public Classifier(ILogger logger)
        {
            _logger = logger;
        }
        public async void Start()
        {
            // initialize
            _classifierStartTime = DateTime.Now;
            DateTime _lastProcessedBlockAt = DateTime.Now;
            DateTime _lastProcessedBlockTimestamp = DateTime.MinValue;

            try
            {
                // get the last recorded processed block
                long nextBlockNumber;
                using (var db = new zeromevContext())
                {
                    nextBlockNumber = await db.GetLastProcessedBlock();
                }
                if (nextBlockNumber < 0)
                {
                    _logger.LogInformation($"zm classifier failed to get last processed block");
                    Stop();
                    return;
                }

                // start from one after the last processed
                nextBlockNumber++;

                _logger.LogInformation($"zm classifier starting at {_classifierStartTime} from block {nextBlockNumber}");

                // classification loop- run until the service is stopped
                while (!_isStopped)
                {
                    try
                    {
                        // poll extractor results for the next block we want to process
                        var zb = DB.GetZMBlock(nextBlockNumber);
                        ZMView zv = null;
                        if (zb != null) // only both the rpc node if we have arrival data
                        {
                            zv = new ZMView(nextBlockNumber);
                            await zv.Refresh(_http, zb);
                        }

                        bool doMoveOn;
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
                            doMoveOn = (DateTime.Now > _lastProcessedBlockAt.AddSeconds(PollTimeoutSecs));
                            if (doMoveOn)
                                _logger.LogInformation($"timeout {nextBlockNumber} " + reason);
                            else
                                _logger.LogInformation($"polling {nextBlockNumber} " + reason);
                        }
                        else
                        {
                            doMoveOn = true;
                        }

                        // move onto the next block
                        if (doMoveOn)
                        {
                            // classify mev if we can (use a single PoP if that is all we have after the timeout period)
                            if (zb != null && zv != null && zv.PoPs != null && zv.PoPs.Count != 0)
                            {
                                await ClassifyMEV(zv);
                                _logger.LogInformation($"classified {nextBlockNumber} PoPs {zv.PoPs.Count}");
                            }

                            using (var db = new zeromevContext())
                            {
                                await db.SetLastProcessedBlock(nextBlockNumber);
                            }

                            _lastProcessedBlockAt = DateTime.Now;
                            nextBlockNumber++;
                            if (zv != null)
                                _lastProcessedBlockTimestamp = zv.BlockTimeAvg;
                        }

                        // give sufficient time for the next block to arrive and our extractors to return data
                        int delaySecs = 0;
                        TimeSpan diff = _lastProcessedBlockTimestamp.AddSeconds(MinimumTimeSinceLastBlockTimestampSecs) - DateTime.Now.ToUniversalTime(); // utc because we are comparing to db block data
                        if (diff.Ticks > 0)
                            delaySecs = (int)diff.TotalSeconds;

                        if (!doMoveOn)
                        {
                            // delay for at least the polling time between unsuccessful attempts
                            if (delaySecs < PollEverySecs)
                                delaySecs = PollEverySecs;
                        }

                        if (delaySecs != 0)
                        {
                            _logger.LogInformation($"waiting {delaySecs} secs for {nextBlockNumber}");
                            await Task.Delay(TimeSpan.FromSeconds(delaySecs));
                        }
                    }
                    catch (Exception ex)
                    {
                        // an unexpected error likely means a database or network failure, and we must not progress onto a new block until this is rectified
                        _logger.LogInformation($"error block {nextBlockNumber}: {ex.ToString()}");
                        await Task.Delay(TimeSpan.FromSeconds(PollEverySecs));
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                Stop();
                // TODO log error and exit
            }
        }

        private async Task ClassifyMEV(ZMView zv)
        {
            using (var db = new zeromevContext())
            {
                foreach (var tx in zv.Txs)
                {
                    DateTime dt = DateTime.SpecifyKind(tx.ArrivalMin, DateTimeKind.Unspecified);
                    db.ZmTimes.Add(new ZmTime() { ArrivalTime = dt, BlockNumber = zv.BlockNumber, TransactionHash = tx.TxnHash, TransactionPosition = tx.TxIndex });
                }
                db.SaveChanges();
            }
        }

        public void Stop()
        {
            _isStopped = true;
        }
    }
}