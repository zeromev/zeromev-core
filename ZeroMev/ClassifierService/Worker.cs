using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroMev.Shared;

namespace ZeroMev.ClassifierService
{
    public class Worker : BackgroundService
    {
        readonly ILogger<Worker> _logger;
        Classifier _classify;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("starting at {time}", DateTimeOffset.Now);

            _classify = new Classifier(_logger);
            _classify.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("exiting at {time}", DateTimeOffset.Now);

            // exit gracefully
            if (_classify != null)
            {
                _classify.Stop();
                await Task.Delay(1000);
            }
        }
    }
}