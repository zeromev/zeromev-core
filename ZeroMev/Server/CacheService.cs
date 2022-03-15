using System;
using System.Text;
using System.Text.Json;
using System.Timers;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.Server
{
    public class CacheService : IHostedService, IDisposable
    {
        const int PollEveryMs = 5000;

        private readonly ILogger<CacheService> _logger;
        private System.Timers.Timer _timer = null!;

        public CacheService(ILogger<CacheService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new System.Timers.Timer(PollEveryMs);
            _timer.AutoReset = true;
            _timer.Elapsed += new ElapsedEventHandler(_timer_Elapsed);
            _timer.Start();

            return Task.CompletedTask;
        }

        private async void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
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
                        foreach (var mb in mevBlocks)
                        {
                            var lb = new MEVLiteBlock(mb.BlockNumber, mb.BlockTime);
                            var zv = new ZMView(mb.BlockNumber);
                            zv.RefreshOffline(null, 10000); // fake the tx count
                            zv.SetMev(mb);
                            lb.MEVLite = BuildMevLite(zv);
                            if (lb.MEVLite.Count > 0)
                            {
                                cache.Blocks.Add(lb);
                                if (cache.Blocks.Count >= MEVLiteCache.CachedMevBlockCount) break;
                            }
                        }
                        DB.MEVLiteCacheJson = JsonSerializer.Serialize<MEVLiteCache>(cache, ZMSerializeOptions.Default);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("error reading mev block data " + ex.ToString());
            }
        }

        public List<MEVLite> BuildMevLite(ZMView zv)
        {
            var r = new List<MEVLite>();
            foreach (ZMTx tx in zv.Txs)
            {
                if (tx.MEV == null || tx.MEVClass == MEVClass.Info) continue;
                r.Add(new MEVLite(tx.MEV));
            }
            return r;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Stop();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}