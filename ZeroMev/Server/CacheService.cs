using System;
using System.Timers;
using ZeroMev.SharedServer;

namespace ZeroMev.Server
{
    public class CacheService : IHostedService, IDisposable
    {
        const int PollEveryMs = 5000;
        const int CachedMevBlockCount = 25;

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

                    // TODO process into mev summaries (RecentBlocksJson)
                    await DB.ReadMevBlocks(latest.Value - CachedMevBlockCount, latest.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("error reading mev block data " + ex.ToString());
            }
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