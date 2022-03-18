/// MIT License
/// Copyright © 2021 pmcgoohan
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
/// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using System.Reactive.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.ExtractorService
{
    public class Extract
    {
        const int RecentBlockCacheSize = 20; // how many recent blocks to cache for uncled block detection 
        const int CheckSyncEveryMs = 5000; // how often to check that the eth client is in sync
        const int MaxSyncLagInBlocks = 3; // the maximum sync lag acceptable before an error condition is declared
        const int MaxGetBlockDataRetries = 3; // the number of retries getting new block data before a connection error is raised

        ILogger _logger;
        Nethereum.Web3.Web3 _web3;
        StreamingWebSocketClient _client;
        EthNewBlockHeadersObservableSubscription _blockSub;
        EthNewPendingTransactionObservableSubscription _pendingSub;
        ConcurrentDictionary<string, TxTimeHash> _txh = new ConcurrentDictionary<string, TxTimeHash>(); // must be concurrent so daily purging does not block and distort arrival times
        BlockTxTimes[] _recentBlocks = new BlockTxTimes[RecentBlockCacheSize];
        int _recentBlockIndex;
        short _extractorIndex;
        DateTime _extractorStartTime;
        long _arrivalCount;
        long _lastBlock;
        static Nethereum.Hex.HexTypes.HexBigInteger _lastFlashbotsBlock;
        bool _isStopped = false;
        DateTime _nextPurge = DateTime.Now.ToUniversalTime().Date.AddDays(1);
        HttpClient _http = new HttpClient();

        public bool HadConnectionException { get; private set; }

        public class BlockTxTimes
        {
            public void Set(long blockNumber, List<TxTimeHash> txTimes)
            {
                BlockNumber = blockNumber;
                TxTimes = txTimes;
            }

            public long BlockNumber { get; private set; }
            public List<TxTimeHash> TxTimes { get; private set; }
        }

        public Extract(ILogger logger)
        {
            _logger = logger;
        }

        public async void Start()
        {
             // initialize
            _extractorStartTime = DateTime.Now.ToUniversalTime();
            _arrivalCount = 0;
            _lastBlock = 0;
            _recentBlockIndex = 0;

            // build the recent block cache to manage uncled blocks (entries are reused)
            for (int i = 0; i < RecentBlockCacheSize; i++)
                _recentBlocks[i] = new BlockTxTimes();

            try
            {
                // get connection strings
                string httpsUri = Config.Settings.EthereumRPC;
                string wssUri = Config.Settings.EthereumWSS;
                _logger.LogInformation($"https connection {httpsUri}");
                _logger.LogInformation($"wss connection {wssUri}");

                // connect to eth node
                _web3 = new Web3(httpsUri);

                // wait until client has synced (non blocking)
                while (1 == 1)
                {
                    var task = await _web3.Eth.Syncing.SendRequestAsync();
                    if (!task.IsSyncing)
                        break;
                    _logger.LogInformation($"waiting for client to sync, start {task.StartingBlock} current {task.CurrentBlock} highest {task.HighestBlock}");
                    await Task.Delay(CheckSyncEveryMs);
                }

                // configure streaming
                _client = new StreamingWebSocketClient(wssUri);
                _client.Error += Client_Error;
                _extractorIndex = Config.Settings.ExtractorIndex;

                // subscribe blocks
                _blockSub = new EthNewBlockHeadersObservableSubscription(_client);
                _blockSub.GetSubscriptionDataResponsesAsObservable().Subscribe(NewBlock);

                // subscribe pending txs
                _pendingSub = new EthNewPendingTransactionObservableSubscription(_client);
                _pendingSub.GetSubscriptionDataResponsesAsObservable().Subscribe(NewPendingTx);

                // wait for data
                _client.StartAsync().Wait();
                _blockSub.SubscribeAsync().Wait();
                _pendingSub.SubscribeAsync().Wait();

                // periodically check that the client is still synced
                while (!_isStopped)
                {
                    // all is well if the client is synced
                    await Task.Delay(CheckSyncEveryMs);
                    var task = await _web3.Eth.Syncing.SendRequestAsync();
                    if (!task.IsSyncing)
                        continue;

                    // tolerate a small lag in block sync, so we don't restart unnecessarily
                    BigInteger syncLag = (BigInteger)task.HighestBlock - (BigInteger)task.CurrentBlock;
                    if (syncLag <= MaxSyncLagInBlocks)
                    {
                        _logger.LogInformation($"tolerating {syncLag} block sync lag, start {task.StartingBlock} current {task.CurrentBlock} highest {task.HighestBlock}");
                        continue;
                    }

                    // a large lag in syncronization must be treated as a fatal error for the current extraction instance, as we may not be able to trust arrival or inclusion times
                    _logger.LogInformation($"client is syncing, start {task.StartingBlock} current {task.CurrentBlock} highest {task.HighestBlock}");
                    Client_Error(this, new Exception("client is syncing"));
                }
            }
            catch (Exception e)
            {
                Stop();
                ConnectionError(this, e);
            }
        }

        public void Stop()
        {
            _isStopped = true;

            if (_blockSub != null) try { _blockSub.UnsubscribeAsync().Wait(); } catch { }
            if (_pendingSub != null) try { _pendingSub.UnsubscribeAsync().Wait(); } catch { }
            if (_client != null) try { _client.StopAsync().Wait(); } catch { }

            _blockSub = null;
            _pendingSub = null;
            _client = null;
        }

        private void NewPendingTx(string txh)
        {
            // record new pending txs as they arrive
            DateTime timestamp = DateTime.Now.ToUniversalTime();
            var tt = new TxTimeHash();
            tt.TxHash = txh;
            tt.ArrivalTime = timestamp;
            tt.ArrivalBlockNumber = _lastBlock;
            if (_txh.TryAdd(txh, tt))
                _arrivalCount++;
        }

        private async void NewBlock(Block block)
        {
            // record block details with low latency
            long blockNumber = (long)(BigInteger)block.Number;
            _lastBlock = blockNumber;
            DateTime timestamp = DateTime.Now.ToUniversalTime();

            // get block txs
            Task<BlockWithTransactions> blockTxsReq = null;
            try
            {
                for (int i = 0; i < MaxGetBlockDataRetries; i++)
                {
                    blockTxsReq = _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(block.Number);
                    blockTxsReq.Wait();

                    if (blockTxsReq.Result != null && blockTxsReq.Result.Transactions != null)
                        break;

                    if (_logger != null) _logger.LogInformation($"getting block data failed for {blockNumber} retry {i}");
                    await Task.Delay(2000);
                }

                // treat as a connection error if all retries failed
                if (blockTxsReq.Result == null || blockTxsReq.Result.Transactions == null)
                {
                    ConnectionError(this, new Exception("getting block data failed after retries"));
                    return;
                }
            }
            catch (Exception e)
            {
                ConnectionError(this, e);
                return;
            }

            // detect whether there is a duplicate block to uncle
            BlockTxTimes uncle = null;
            int uncleIndex = -1;
            for (int i = 0; i < RecentBlockCacheSize; i++)
                if (_recentBlocks[i].BlockNumber == blockNumber)
                {
                    uncle = _recentBlocks[i];
                    uncleIndex = i;
                    break;
                }

            BlockWithTransactions btxs = blockTxsReq.Result;
            List<TxTimeHash> tts = new List<TxTimeHash>(btxs.Transactions.Length);

            // restore transactions for the uncled block if there is one (any included in the new block will be removed below)
            if (uncle != null)
            {
                foreach (TxTimeHash tt in uncle.TxTimes)
                    _txh.TryAdd(tt.TxHash, tt);

                if (_logger != null) _logger.LogInformation($"uncled block detected {uncle.BlockNumber}");
            }

            // iterate new block txs
            for (int i = 0; i < btxs.Transactions.Length; i++)
            {
                // sanity check block transaction index
                var t = btxs.Transactions[i];
                if (t.TransactionIndex.Value != i)
                    throw new Exception("bad transaction index");

                TxTimeHash tt;
                if (!_txh.TryGetValue(t.TransactionHash, out tt))
                {
                    // if we haven't seen this tx before, it has likely been sent direct to the miner (Flashbots, MistX, Mining pool, etc)
                    // use the same timestamp and blocknumber values as the block to indicate this
                    // do this rather than zeroing these fields to remain accurate even after an uncle attack
                    tt = new TxTimeHash();
                    tt.TxHash = t.TransactionHash;
                    tt.ArrivalTime = timestamp;
                    tt.ArrivalBlockNumber = blockNumber;
                    _arrivalCount++;
                }
                else
                {
                    // if this is a known tx, it has been included now so free up the entry
                    _txh.Remove(t.TransactionHash, out _);
                }

                tts.Add(tt);
            }

            // replace the now uncled block (if there is one) with the new block or add it to the buffer if not
            if (uncle != null)
                _recentBlocks[uncleIndex].Set(blockNumber, tts);
            else
            {
                _recentBlocks[_recentBlockIndex++].Set(blockNumber, tts);
                if (_recentBlockIndex >= RecentBlockCacheSize)
                    _recentBlockIndex = 0;
            }

            int pendingCount = _txh.Count;

            // non blocking write to the db
            DB.QueueWriteExtractorBlockAsync(new ExtractorBlock(
                blockNumber,
                _extractorIndex,
                timestamp,
                _extractorStartTime,
                _arrivalCount,
                pendingCount,
                tts));

            // non blocking collection and write of flashbots data (if this is a new block)
            if (block.Number != _lastFlashbotsBlock && Config.Settings.DoExtractFlashbots)
            {
                _ = FlashbotsAPI.Collect(_http, 4000); // delay by a few seconds to give them a chance to update the api with the new block
                _lastFlashbotsBlock = block.Number;
            }

            if (_logger != null) _logger.LogInformation($"new block {block.Number} size {tts.Count} arrivals {_arrivalCount} pending {pendingCount}");

            // purge pending transactions daily (we cannot tell between txs that are censored and cancelled until address/nonce data is recorded with pending txs, so until then old pendings must be expired)
            if (DateTime.Now.ToUniversalTime() > _nextPurge)
            {
                PurgePending();
                _nextPurge = DateTime.Now.ToUniversalTime().Date.AddDays(1);
            }
        }

        private void Client_Error(object sender, Exception e)
        {
            ConnectionError(sender, e);
        }

        private void ConnectionError(object sender, Exception e)
        {
            HadConnectionException = true;
            if (_logger != null) _logger.LogInformation($"extractor client error {e.Message}");
        }

        private void PurgePending()
        {
            DateTime purgeBefore = DateTime.Now.ToUniversalTime().AddDays(-Config.Settings.PurgeAfterDays);
            List<string> purge = new List<string>();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // build a list of old pending txs to purge
            foreach (TxTimeHash tt in _txh.Values)
                if (tt.ArrivalTime < purgeBefore)
                    purge.Add(tt.TxHash);

            // remove them from the dictionary
            if (purge != null)
                foreach (string tx in purge)
                    _txh.TryRemove(tx, out _);

            sw.Stop();

            _logger.LogInformation($"purged {purge.Count} out of {_txh.Count} pending txs in {sw.ElapsedMilliseconds} ms");
        }
    }
}