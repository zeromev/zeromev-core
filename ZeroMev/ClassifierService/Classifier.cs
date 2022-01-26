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
        const int PollEveryMs = 4000;
        const int PollTimeoutMs = PollEveryMs * 4;

        ILogger _logger;
        bool _isStopped = false;
        DateTime _classifierStartTime;

        public bool HadConnectionException { get; private set; }

        public Classifier(ILogger logger)
        {
            _logger = logger;
        }
        public async void Start()
        {
            // initialize
            _classifierStartTime = DateTime.Now;
            DateTime _lastProcessedBlock = DateTime.Now;

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
                    // TODO log error and exit
                }

                // start from one after this
                nextBlockNumber++;

                // classification loop- run until the service is stopped
                while (!_isStopped)
                {
                    try
                    {
                        // poll extractor results for the next block we want to process
                        var zb = DB.GetZMBlock(nextBlockNumber);
                        var zv = new ZMView(nextBlockNumber);
                        zv.Refresh(null);

                        bool doMoveOn;
                        if (zb == null || zb.UniquePoPCount() < 2)
                        {
                            // a successful attempt is at least 2 PoPs (extractors) providing data
                            // pause between polling if this criteria is not met
                            // or after a longer timeout period, move on anyway
                            doMoveOn = (DateTime.Now > _lastProcessedBlock.AddMilliseconds(PollTimeoutMs));
                        }
                        else
                        {
                            // classify mev and move onto the next block on success
                            ClassifyMEV(zb);
                            doMoveOn = true;
                        }

                        // move onto the next block
                        if (doMoveOn)
                        {
                            using (var db = new zeromevContext())
                            {
                                await db.SetLastProcessedBlock(nextBlockNumber);
                                nextBlockNumber++;
                                _lastProcessedBlock = DateTime.Now;
                            }
                        }
                        else
                        {
                            await Task.Delay(PollEveryMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        // an unexpected error likely means a database or network failure, and we must not progress onto a new block until this is rectified
                        // TODO log
                        await Task.Delay(PollEveryMs);
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

        private void ClassifyMEV(ZMBlock zb)
        {

        }

        public void Stop()
        {
            _isStopped = true;
        }
    }
}