using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        public const int BlocksPerPage = 9;
        public const int JumpBlocksPerPage = BlocksPerPage + 1;
        public const long EarliestZMBlock = 13359564;
        public const long EarliestFlashbotsBlock = 11834049;
        public const int ExpireRecentCacheSecs = 5;
        public const int RecentBlockSecs = 60;

        public static async Task<ZMBlock?> GetZMBlock(HttpClient http, long blockNumber)
        {
            return await http.GetFromJsonAsync<ZMBlock>(@"zmblock/" + blockNumber, ZMSerializeOptions.Default);
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
    }
}