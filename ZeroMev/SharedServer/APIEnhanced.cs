using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public static class APIEnhanced
    {
        public const string JsonEthGetBlockReceiptsByNumber = "{\"id\":1,\"jsonrpc\":\"2.0\",\"method\":\"eth_getBlockReceipts\",\"params\":[\"{0}\",true]}";

        public static async Task<GetBlockReceiptsByNumber?> GetBlockReceiptsByNumber(HttpClient http, string blockNumberHexOrInt)
        {
            string jsonReq = JsonEthGetBlockReceiptsByNumber.Replace("{0}", blockNumberHexOrInt);
            var httpContent = new StringContent(jsonReq, System.Text.Encoding.UTF8, "application/json");

            var getBlockTask = await http.PostAsync(Config.Settings.EthereumRPC, httpContent);
            string? result = await getBlockTask.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<GetBlockReceiptsByNumber>(result);
        }
    }

    public class GetBlockReceiptsByNumber
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("result")]
        public List<TransactionReceipt> Result { get; set; }
    }

    public class TransactionReceipt
    {
        [JsonPropertyName("blockHash")]
        public string BlockHash { get; set; }

        [JsonPropertyName("blockNumber")]
        public string BlockNumber { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("transactionIndex")]
        public string TransactionIndex { get; set; }
    }
}