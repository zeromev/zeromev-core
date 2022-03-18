using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public static class ConfigBuilder
    {
        public static void Build()
        {
            var config = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

            Config.Settings = config.GetSection("AppSettings").Get<AppSettings>();
        }
    }
}