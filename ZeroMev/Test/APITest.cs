using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.Test
{
    [TestClass]
    public class APITest
    {
        [TestMethod]
        public async Task GetTxByHash()
        {
            string txh = "0x335abab242917e4a07596f913ecfdc47571497370c83a126bd3cd4a844ca1c02";
            HttpClient http = new HttpClient();
            var r = await API.GetTxByHash(http, txh);
            Assert.AreEqual(r.Result.Hash, txh);
        }

        [TestMethod]
        public async Task GetAccountByAddress()
        {
            string address = "0x006C769062F1CD5E928c5B3d6B7b64ac96e8D87b";
            HttpClient http = new HttpClient();
            var r = await API.GetAccountByAddress(http, address, 1, 25);
            Assert.AreEqual(r.Status, "1");
        }

        [TestMethod]
        public async Task GetBlockByNumber()
        {
            long blockNumber = 13850536;
            HttpClient http = new HttpClient();
            var r = await API.GetBlockByNumber(http, blockNumber);
            Assert.AreEqual(Num.HexToLong(r.Result.Number), blockNumber);
        }

        [TestMethod]
        public async Task GetBlockTransactionCountByNumber()
        {
            long blockNumber = 13850536;
            HttpClient http = new HttpClient();
            var txCount = await API.GetBlockTransactionCountByNumber(http, blockNumber);
            Assert.AreEqual(txCount.Value, 282);
        }

        [TestMethod]
        public async Task GetBlockTransactionStatus()
        {
            long blockNumber = 13850536;
            HttpClient http = new HttpClient();
            var r = await APIEnhanced.GetBlockTransactionStatus(http, blockNumber.ToString());
            Assert.AreEqual(r.Count, 282);
        }
    }
}