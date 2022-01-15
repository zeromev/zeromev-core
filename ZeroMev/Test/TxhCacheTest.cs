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
    public class TxhCacheTest
    {
        [TestMethod]
        public async Task TestGet()
        {
            HttpClient httpClient = new HttpClient();
            TxhCache txhCache = new TxhCache();

            // test api
            var a = await txhCache.Get(httpClient, "0x125a3d1dbebec89aad2c6c0fa451d3784e1d02bc4d4f792779a83eaec64f448e");
            Assert.IsTrue(a.BlockNumber == 13785855);
            Assert.IsTrue(a.TxIndex == 214);

            // test caching
            var b = await txhCache.Get(httpClient, "0x125a3d1dbebec89aad2c6c0fa451d3784e1d02bc4d4f792779a83eaec64f448e");
            Assert.IsTrue(object.ReferenceEquals(a, b));

            // test non existance
            var c = await txhCache.Get(httpClient, "0x125a3d1dbebec89aad2c6c0fa451d3784e1d02bc4d4f792779a83eaec64f448f");
            Assert.IsTrue(c.APIResult == APIResult.Retry);

            // test bad format
            var d = await txhCache.Get(httpClient, "0x125a3d1dbebec89aad2c6c0fa451d3784e1d02bc4d4f792779a83eaec64f448P");
            Assert.IsTrue(d.APIResult == APIResult.NoData);
        }
    }
}