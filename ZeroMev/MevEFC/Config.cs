using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;


namespace ZeroMev.MevEFC
{
    public static class Config
    {
        static IConfiguration? _config;

        public static string Get(string key)
        {
            if (_config == null)
                _config = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();

            return _config[key];
        }
    }
}