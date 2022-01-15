using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public class FlashbotsAPI
    {
        public const string UrlFlashbotsRecent = @"https://blocks.flashbots.net/v1/blocks?limit=5";
        public const string UrlFlashbotsBlock = @"https://blocks.flashbots.net/v1/blocks?block_number=";
        public const string UrlFlashbotsAllBlocks = @"https://blocks.flashbots.net/v1/all_blocks";

        public static async Task Collect(HttpClient http, int preDelayMs)
        {
            await Task.Delay(preDelayMs);
            var r = await FlashbotsAPI.GetFlashbotsRecent(http);
            DB.QueueWriteFlashbotsBlocksAsync(r.blocks);
        }

        public static async Task<FBRoot?> GetFlashbotsRecent(HttpClient http)
        {
            return await http.GetFromJsonAsync<FBRoot>(UrlFlashbotsRecent, ZMSerializeOptions.Default);
        }
        public static async Task<FBRoot?> GetFlashbotsBlockByNumber(HttpClient http, long blockNumber)
        {
            string url = UrlFlashbotsBlock + blockNumber;
            return await http.GetFromJsonAsync<FBRoot>(url, ZMSerializeOptions.Default);
        }
        public static async Task<List<FBBlock>?> GetFlashbotsAll(HttpClient http)
        {
            return await http.GetFromJsonAsync<List<FBBlock>>(UrlFlashbotsAllBlocks, ZMSerializeOptions.Default);
        }

        public static BitArray ConvertBundlesToBitArray(FBBlock fb)
        {
            BitArray ba = new BitArray(fb.transactions.Count);

            int bundle_index = 0;
            int tx_index = 0;

            for (int i = 0; i < fb.transactions.Count; i++)
            {
                FBTx tx = fb.transactions[i];
                if (tx.bundle_index > bundle_index)
                {
                    bundle_index = tx.bundle_index;
                    tx_index = 0;
                    ba.Set(i, true); // mark as a new bundle
                }
                if (tx.bundle_index != bundle_index || tx.tx_index != tx_index) return null; // bad data
                tx_index++;
            }

            return ba;
        }
    }
}