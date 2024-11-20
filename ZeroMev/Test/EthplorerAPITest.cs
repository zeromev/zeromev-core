using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using ZeroMev.SharedServer;
using System.Diagnostics;
using ZeroMev.MevEFC;
using ZeroMev.ClassifierService;

namespace ZeroMev.Test
{
    [TestClass]
    public class EthplorerAPITest
    {
        [TestMethod]
        public async Task GetTokenInfo()
        {
            HttpClient http = new HttpClient();
            var r = await EthplorerAPI.GetTokenInfo(http, "0xdac17f958d2ee523a2206206994597c13d831ec7");
            Assert.AreEqual(r.Symbol, "USDT");
        }

        [TestMethod]
        public async Task GetTokensNew()
        {
            HttpClient http = new HttpClient();
            var r = await EthplorerAPI.GetTokensNew(http);
            foreach (var t in r)
                Debug.WriteLine($"{t.Name} {t.Symbol} {t.Decimals}");
        }

        [TestMethod]
        public async Task UpdateNewTokens()
        {
            HttpClient http = new HttpClient();
            await Classifier.UpdateNewTokens(http);
        }
    }
}