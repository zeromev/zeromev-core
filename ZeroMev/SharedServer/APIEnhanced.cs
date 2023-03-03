using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ZeroMev.Shared;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ZeroMev.SharedServer
{
    public class GetBlockWithTransactionStatusResult
    {
        public BitArray? TxStatus;
        public string[,] Addresses;
    }

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

        public static async Task<GetBlockWithTransactionStatusResult> GetBlockWithTransactionStatus(HttpClient http, string blockNumberHexOrInt)
        {
            var blockTask = API.GetBlockByNumber(http, blockNumberHexOrInt);
            var txStatus = await GetBlockTransactionStatus(http, blockNumberHexOrInt);
            var block = await blockTask;
            return new GetBlockWithTransactionStatusResult { TxStatus = txStatus, Addresses = AddressesStringFromBlock(block) };
        }

        public static string[,] AddressesStringFromBlock(GetBlockByNumber block)
        {
            if (block == null || block.Result == null || block.Result.Transactions == null) return null;
            if (block.Result.Transactions.Count == 0) return new string[,] { };
            string[,] addresses = new string[block.Result.Transactions.Count, 2];
            for (int i = 0; i < block.Result.Transactions.Count; i++)
            {
                addresses[i, 0] = block.Result.Transactions[i].From ?? "".ToLower();
                addresses[i, 1] = block.Result.Transactions[i].To ?? "".ToLower();
            }
            return addresses;
        }

        public static byte[] AddressesToBytes(string[,] addresses)
        {
            if (addresses == null) return null;
            if (addresses.Length == 0) return new byte[] { };
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i <= addresses.GetUpperBound(0); i++)
            {
                sb.Append(addresses[i, 0]);
                sb.Append(",");
                sb.Append(addresses[i, 1]);
                sb.Append(",");
            }
            sb.ToString(0, sb.Length - 1);
            return Binary.Compress(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        public static string[,] BytesToAddresses(byte[] bytes)
        {
            if (bytes == null) return null;
            if (bytes.Length == 0) return new string[,] { };
            var csv = Encoding.ASCII.GetString(Binary.Decompress(bytes));
            var addressCsv = csv.Split(",");
            string[,] addresses = new string[csv.Length / 2, 2];
            bool isTo = false;
            int row = 0;
            for (int i = 0; i < addressCsv.Length; i++)
            {
                addresses[row, isTo ? 1 : 0] = addressCsv[i];
                if (isTo) row++;
                isTo = !isTo;
            }
            return addresses;
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