using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using ZeroMev.Shared;


namespace ZeroMev.Shared
{
    public class API
    {
        public const string EtherscanAPIKey = @"W4RX5RB6WEVZPTZ6YPP8MFVWU7WCVM2TY2";
        public const string UrlInfura = @"https://mainnet.infura.io/v3/0a619b80a59e4a838d37fbf4e8df5681";
        public const string UrlGetTransactionByHash = @"https://api.etherscan.io/api?module=proxy&action=eth_getTransactionByHash&txhash={0}&apikey=" + EtherscanAPIKey;
        public const string UrlGetAccountByAddress = @"https://api.etherscan.io/api?module=account&action=txlist&address={0}&startblock=0&endblock=99999999&sort=desc&page=1&offset=1000&apikey=" + EtherscanAPIKey;
        public const string JsonEthGetBlockByNumber = "{\"id\":1,\"jsonrpc\":\"2.0\",\"method\":\"eth_getBlockByNumber\",\"params\":[\"{0}\",true]}";
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
            return await http.GetFromJsonAsync<GetTxnByHash>(string.Format(API.UrlGetTransactionByHash, fromTxh));
        }

        public static async Task<TxList?> GetAccountByAddress(HttpClient http, string address)
        {
            return await http.GetFromJsonAsync<TxList>(string.Format(API.UrlGetAccountByAddress, address));
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

            var getBlockTask = await http.PostAsync(UrlInfura, httpContent);
            string? result = await getBlockTask.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<GetBlockByNumber>(result);
        }
    }
}