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
        static IConfigurationRoot _config;

        public static void Build()
        {
            _config = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

            Config.Settings = _config.GetSection("AppSettings").Get<AppSettings>();
        }

        public static IConfigurationSection GetSection(string sectionName)
        {
            return _config.GetSection(sectionName);
        }
    }
}