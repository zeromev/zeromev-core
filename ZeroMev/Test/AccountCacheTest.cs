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
    public class AccountCacheTest
    {
        [TestMethod]
        public async Task TestGet()
        {
            HttpClient httpClient = new HttpClient();
            AccountCache cache = new AccountCache();

            // test valid get
            var a = await cache.Get(httpClient, "0x12ea0650fF68af0f2581C9683D6ea1F353fEC343", 1, 25);
            Assert.IsTrue(a.Status == "1");
            Assert.IsTrue(a.Result.Count >= 11);

            // test invalid get
            var b = await cache.Get(httpClient, "0x12ea0650fF68af0f2581C9683D6ea1F353fEC34P", 5, 25);
            Assert.IsNull(b);
        }
    }
}