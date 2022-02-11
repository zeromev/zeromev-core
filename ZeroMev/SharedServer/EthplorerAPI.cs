using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using ZeroMev.Shared;
using ZeroMev.MevEFC;
using EFCore.BulkExtensions;

namespace ZeroMev.SharedServer
{
    public class EthplorerAPI
    {
        private const string UrlGetTokenInfo = @"https://api.ethplorer.io/getTokenInfo/{0}?apiKey={1}";
        private const string UrlGetTokensNew = @"https://api.ethplorer.io/getTokensNew?apiKey={0}";

        public static async Task<ZmToken?> GetTokenInfo(HttpClient http, string tokenAddress)
        {
            return await http.GetFromJsonAsync<ZmToken>(string.Format(UrlGetTokenInfo, tokenAddress, Config.Settings.EthplorerAPIKey), ZMSerializeOptions.StringToInt);
        }

        public static async Task<ZmToken[]?> GetTokensNew(HttpClient http)
        {
            return await http.GetFromJsonAsync<ZmToken[]>(string.Format(UrlGetTokensNew, Config.Settings.EthplorerAPIKey), ZMSerializeOptions.StringToInt);
        }

        public static async Task<bool> UpdateNewTokens(HttpClient http)
        {
            try
            {
                var tokens = await GetTokensNew(http);
                using (var db = new zeromevContext())
                {
                    await db.BulkInsertOrUpdateAsync<ZmToken>(tokens);
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}