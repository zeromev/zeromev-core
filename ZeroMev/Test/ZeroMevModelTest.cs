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
    public class ZeroMevModelTest
    {
        public async Task TestZMViewRefresh()
        {
            // TODO Mock BaseAddress
            HttpClient http = new HttpClient();
            http.BaseAddress = new Uri(@"https://localhost:7119/");
            ZMView zv = new ZMView(13850539);
            var success = await zv.Refresh(http);
            Assert.IsTrue(success);
            Assert.AreEqual(zv.BlockHash, "0x3e1b2687b534f8d026ad6187d9d8cdb6862b3c8f7cec5809d81f625e5eb251d5");
        }
    }
}