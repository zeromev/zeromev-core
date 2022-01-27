using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroMev.Shared
{
    // embed config to avoid needing config libraries in WASM and because environment vars are not relevant to WASM clients
    // non-WASM clients can set these from their configuration to connect to non-default providers
    public static class APIConfig
    {
        public static string EtherscanAPIKey = @"W4RX5RB6WEVZPTZ6YPP8MFVWU7WCVM2TY2";
        public static string EthereumRPC = @"https://mainnet.infura.io/v3/0a619b80a59e4a838d37fbf4e8df5681";
    }
}