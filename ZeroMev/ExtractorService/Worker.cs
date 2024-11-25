using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroMev.Shared;

namespace ZeroMev.ExtractorService
{
    public class Worker : BackgroundService
    {
        readonly ILogger<Worker> _logger;
        Extract _extract;
        TimeSpan _timeout = new TimeSpan(0, 0, 25);

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("starting at {time}", DateTimeOffset.Now);

            _extract = new Extract(_logger);
            _extract.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);

                // flag to reconnect after a new pending transaction timeout
                if (_extract.LastTransactionTime != null && (DateTime.Now - _extract.LastTransactionTime) > _timeout)
                {
                    _logger.LogInformation("transaction timeout - requesting reconnect");
                    _extract.HadConnectionException = true;
                }

                // flag to reconnect after a new block timeout
                if (_extract.LastBlockTime != null && (DateTime.Now - _extract.LastBlockTime) > _timeout)
                {
                    _logger.LogInformation("block timeout - requesting reconnect");
                    _extract.HadConnectionException = true;
                }

                // if there have been connectivity issues, retry connection with a fresh extractor
                if (_extract.HadConnectionException)
                {
                    _logger.LogInformation("stopping to reconnect at {time}", DateTimeOffset.Now);
                    _extract.Stop();
                    _extract = new Extract(_logger);
                    await Task.Delay(1000, stoppingToken);
                    _logger.LogInformation("reconnection attempt {time}", DateTimeOffset.Now);
                    _extract.Start();
                }
            }

            _logger.LogInformation("exiting at {time}", DateTimeOffset.Now);

            // exit gracefully
            if (_extract != null)
            {
                _extract.Stop();
                await Task.Delay(1000);
            }
        }
    }
}