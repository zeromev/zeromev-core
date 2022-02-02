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
        const int PollEverySecs = 6;
        const int PollTimeoutSecs = 180;
        const int MinimumTimeSinceLastBlockTimestampSecs = 24; // give our extractors time to return data

        readonly ILogger<Classifier> _logger;

        bool _isStopped = false;
        DateTime _classifierStartTime;
        static HttpClient _http = new HttpClient();

        public Classifier(ILogger<Classifier> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                        if (zb != null) // only bother the rpc node if we have arrival data
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
                            using (var db = new zeromevContext())
                            {
                                if (zb != null && zv != null && zv.PoPs != null && zv.PoPs.Count != 0)
                                {
                                    await WriteTxTimes(zv, db);
                                    _logger.LogInformation($"classified {nextBlockNumber} PoPs {zv.PoPs.Count}");
                                }

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

                        // unless this block has already written to the db, in which case skip
                        if (ex != null && ex.InnerException != null && ex.InnerException.Message.Contains("duplicate key"))
                            nextBlockNumber++;

                        await Task.Delay(TimeSpan.FromSeconds(PollEverySecs));
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                // TODO log error and exit
                _logger.LogInformation($"error: " + e.ToString());
            }

            _logger.LogInformation($"zm classifier stopping (started {_classifierStartTime})");
        }

        public static async Task WriteTxTimes(ZMView zv, zeromevContext db)
        {
            foreach (var tx in zv.Txs)
            {
                DateTime dt = DateTime.SpecifyKind(tx.ArrivalMin, DateTimeKind.Unspecified);
                db.ZmTimes.Add(new ZmTime() { ArrivalTime = dt, BlockNumber = zv.BlockNumber, TransactionHash = tx.TxnHash, TransactionPosition = tx.TxIndex });
            }
            db.SaveChanges();
        }

        public static async Task ClassifyMEV(long blockNumber, zeromevContext db)
        {
            // get every swap in the passed block with time data
            var attackers = from a in db.Swaps
                            join at in db.ZmTimes on a.TransactionHash equals at.TransactionHash
                            where a.BlockNumber == blockNumber
                            orderby a.AbiName, a.Protocol, a.TokenInAddress, a.TokenOutAddress, a.BlockNumber, a.TransactionPosition
                            select new { a.TransactionHash, a.TraceAddress, a.AbiName, a.Protocol, a.TokenInAddress, a.TokenOutAddress, a.TokenInAmount, a.TokenOutAmount, a.BlockNumber, a.TransactionPosition, at.ArrivalTime };

            // find swaps of the same kind
            var frontruns = from v in db.Swaps
                            from vt in db.ZmTimes.Where(vt => v.TransactionHash == vt.TransactionHash)
                            from a in attackers.Where(
                                  // where the victim arrived before the attacker
                                  sw => vt.ArrivalTime < sw.ArrivalTime

                                  // but was included after them in the chain
                                  && (
                                      vt.BlockNumber > sw.BlockNumber ||
                                      (vt.BlockNumber == sw.BlockNumber && vt.TransactionPosition > sw.TransactionPosition)
                                     )

                                  // for the same pair (buys and sells)
                                  && (
                                      (v.TokenInAddress == sw.TokenInAddress && v.TokenOutAddress == sw.TokenOutAddress)
                                      ||
                                      (v.TokenInAddress == sw.TokenOutAddress && v.TokenOutAddress == sw.TokenInAddress)
                                     )

                                  // and on the same DEX
                                  && (v.AbiName == sw.AbiName && v.Protocol == sw.Protocol)
                              )
                            orderby v.AbiName, v.Protocol, a.TokenInAddress, a.TokenOutAddress, vt.ArrivalTime, a.BlockNumber, a.TransactionPosition
                            select new { a.TransactionHash, a.TokenInAddress, a.TokenOutAddress, vTokenInAddress = v.TokenInAddress, vTokenOutAddress = v.TokenOutAddress, TradesWith = (v.TokenInAddress == a.TokenInAddress), a.TokenInAmount, a.TokenOutAmount, vTokenInAmount = v.TokenInAmount, vTokenOutAmount = v.TokenOutAmount, Price = Price(a.TokenInAmount, a.TokenOutAmount, true), vPrice = Price(v.TokenInAmount, v.TokenOutAmount, (v.TokenInAddress == a.TokenInAddress)), a.ArrivalTime, vArrivalTime = vt.ArrivalTime, a.BlockNumber, vBlockNumber = v.BlockNumber, a.TransactionPosition, vTransactionPosition = v.TransactionPosition, TxHash = v.TransactionHash, a.TraceAddress, vTxHash = v.TransactionHash, vTraceAddress = v.TraceAddress, a.AbiName, a.Protocol };

            foreach (var f in frontruns)
            {
                Console.WriteLine(f.ToString());
            }
        }

        private static decimal Price(decimal inAmount, decimal outAmount, bool tradesWith)
        {
            if (tradesWith)
                return inAmount / outAmount;
            return outAmount / inAmount;
        }
    }
}