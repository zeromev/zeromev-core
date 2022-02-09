using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using ZeroMev.Shared;
using ZeroMev.MevEFC;

namespace ZeroMev.SharedServer
{
    public class EthplorerAPI
    {
        private const string UrlGetTokenInfo = @"https://api.ethplorer.io/getTokenInfo/{0}?apiKey={1}";
        private const string UrlGetTokensNew = @"https://api.ethplorer.io/getTokensNew?apiKey={0}";

        public static async Task<ZmToken?> GetTokenInfo(HttpClient http, string tokenAddress)
        {
            return await http.GetFromJsonAsync<ZmToken>(string.Format(UrlGetTokenInfo, tokenAddress, DB.GetConfig("ZM_ETHPLORER")), ZMSerializeOptions.StringToInt);
        }

        public static async Task<ZmToken[]?> GetTokensNew(HttpClient http)
        {
            return await http.GetFromJsonAsync<ZmToken[]>(string.Format(UrlGetTokensNew, DB.GetConfig("ZM_ETHPLORER")), ZMSerializeOptions.StringToInt);
        }
    }
}