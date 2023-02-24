using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;

namespace ZeroMev.Shared
{
    public class API
    {
        public const string UrlGetTransactionByHash = @"https://api.etherscan.io/api?module=proxy&action=eth_getTransactionByHash&txhash={0}&apikey={1}";
        public const string UrlGetAccountByAddress = @"https://api.etherscan.io/api?module=account&action=txlist&address={0}&startblock=0&endblock=99999999&sort=desc&page={1}&offset={2}&apikey={3}";
        public const string JsonEthGetBlockByNumber = "{\"id\":1,\"jsonrpc\":\"2.0\",\"method\":\"eth_getBlockByNumber\",\"params\":[\"{0}\",true]}";
        public const string JsonEthGetBlockTransactionCountByNumber = "{\"id\":1,\"jsonrpc\":\"2.0\",\"method\":\"eth_getBlockTransactionCountByNumber\",\"params\":[\"{0}\"]}";
        public const string QuotaExceeded = "quota exceeded";
        public const int BlocksPerPage = 9;
        public const int JumpBlocksPerPage = BlocksPerPage + 1;
        public const long EarliestZMBlock = 13358564;
        public const long EarliestMevBlock = 9216000;
        public const int ExpireRecentCacheSecs = 5;
        public const int RecentBlockSecs = 60;

        public static async Task<string> GetZMBlockJson(HttpClient http, long blockNumber)
        {
            try
            {
                return await http.GetStringAsync(Config.Settings.ZeromevAPI + @"zmblock/" + blockNumber);
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return QuotaExceeded;
                throw ex;
            }
        }

        public static ZMBlock? ReadZMBlockJson(string json, out bool isQuotaExceeded)
        {
            if (json.Contains(QuotaExceeded))
            {
                isQuotaExceeded = true;
                return null;
            }
            isQuotaExceeded = false;
            return JsonSerializer.Deserialize<ZMBlock>(json, ZMSerializeOptions.Default);
        }

        public static async Task<MEVLiteCache?> GetMEVLiteCache(HttpClient http, long? lastBlockNumber)
        {
            try
            {
                string url = Config.Settings.ZeromevAPI + @"zmsummary" + (lastBlockNumber != null ? "/" + lastBlockNumber.ToString() : "");
                var json = await http.GetStringAsync(url);
                if (json == "null" || json.Contains(QuotaExceeded))
                    return null;
                return JsonSerializer.Deserialize<MEVLiteCache>(json, ZMSerializeOptions.Default);
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return null;
                throw ex;
            }
        }

        public static async Task<GetTxnByHash?> GetTxByHash(HttpClient http, string fromTxh)
        {
            return await http.GetFromJsonAsync<GetTxnByHash>(string.Format(API.UrlGetTransactionByHash, fromTxh, Config.Settings.EtherscanAPIKey));
        }

        public static async Task<TxList?> GetAccountByAddress(HttpClient http, string address, int page, int offset)
        {
            return await http.GetFromJsonAsync<TxList>(string.Format(API.UrlGetAccountByAddress, address, page, offset, Config.Settings.EtherscanAPIKey));
        }

        public static async Task<GetBlockByNumber?> GetBlockByNumber(HttpClient http, long blockNumber)
        {
            string hexBlock = Num.LongToHex(blockNumber).ToLower();
            return await GetBlockByNumber(http, hexBlock);
        }

        public static async Task<GetBlockByNumber?> GetBlockByNumber(HttpClient http, string hexBlockNumber)
        {
            string jsonReq = JsonEthGetBlockByNumber.Replace("{0}", hexBlockNumber);
            var httpContent = new StringContent(jsonReq, System.Text.Encoding.UTF8, "application/json");

            var getBlockTask = await http.PostAsync(Config.Settings.EthereumRPC, httpContent);
            string? result = await getBlockTask.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<GetBlockByNumber>(result);
        }

        public static async Task<int?> GetBlockTransactionCountByNumber(HttpClient http, long blockNumber)
        {
            string hexBlock = Num.LongToHex(blockNumber).ToLower();
            return await GetBlockTransactionCountByNumber(http, hexBlock);
        }

        public static async Task<int?> GetBlockTransactionCountByNumber(HttpClient http, string hexBlockNumber)
        {
            string jsonReq = JsonEthGetBlockTransactionCountByNumber.Replace("{0}", hexBlockNumber);
            var httpContent = new StringContent(jsonReq, System.Text.Encoding.UTF8, "application/json");

            var getBlockTask = await http.PostAsync(Config.Settings.EthereumRPC, httpContent);
            string? result = await getBlockTask.Content.ReadAsStringAsync();
            var r = System.Text.Json.JsonSerializer.Deserialize<GetBlockTransactionCountByNumber>(result);
            if (r == null || r.Result == null) return null;
            return Num.HexToInt(r.Result);
        }

        public static async Task<int?> GetBlockTransactionReceipts(HttpClient http, string hexBlockNumber)
        {
            string jsonReq = JsonEthGetBlockTransactionCountByNumber.Replace("{0}", hexBlockNumber);
            var httpContent = new StringContent(jsonReq, System.Text.Encoding.UTF8, "application/json");

            var getBlockTask = await http.PostAsync(Config.Settings.EthereumRPC, httpContent);
            string? result = await getBlockTask.Content.ReadAsStringAsync();
            var r = System.Text.Json.JsonSerializer.Deserialize<GetBlockTransactionCountByNumber>(result);
            if (r == null || r.Result == null) return null;
            return Num.HexToInt(r.Result);
        }

    }
}