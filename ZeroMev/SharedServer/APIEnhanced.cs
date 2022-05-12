using System;
using System.Collections;
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
        public const string JsonEthGetBlockReceiptsByNumber = "{\"id\":1,\"jsonrpc\":\"2.0\",\"method\":\"eth_getBlockReceipts\",\"params\":[\"{0}\"]}";

        public static async Task<GetBlockReceiptsByNumber?> GetBlockReceiptsByNumber(HttpClient http, string blockNumberHexOrInt)
        {
            string jsonReq = JsonEthGetBlockReceiptsByNumber.Replace("{0}", blockNumberHexOrInt);
            var httpContent = new StringContent(jsonReq, System.Text.Encoding.UTF8, "application/json");

            var getBlockTask = await http.PostAsync(Config.Settings.EthereumRPC, httpContent);
            string? result = await getBlockTask.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<GetBlockReceiptsByNumber>(result);
        }

        public static async Task<BitArray?> GetBlockTransactionStatus(HttpClient http, string blockNumberHexOrInt)
        {
            var r = await GetBlockReceiptsByNumber(http, blockNumberHexOrInt);
            if (r == null || r.Result == null) return null;

            BitArray? status = new BitArray(r.Result.Count);
            for (int i = 0; i < r.Result.Count; i++)
                status[i] = r.Result[i].Status != "0x0";
            return status;
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
    }
}