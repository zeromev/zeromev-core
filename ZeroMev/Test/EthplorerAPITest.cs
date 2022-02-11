using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using ZeroMev.SharedServer;
using System.Diagnostics;
using ZeroMev.MevEFC;

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
            await EthplorerAPI.UpdateNewTokens(http);
        }

        [TestMethod]
        public async Task ImportAllTokens()
        {
            // not a test- this is a one off batch process to import all known tokens into the db
            return;

            // before running, populate zm_tokens table with unknown tokens using this SQL:
            // insert into zm_tokens select distinct token_in_address from swaps on conflict(address) do nothing;
            // insert into zm_tokens select distinct token_out_address from swaps on conflict(address) do nothing;

            // get tokens with missing details
            List<ZmToken> missing;
            using (var db = new zeromevContext())
            {
                missing = (from t in db.ZmTokens
                           where t.Symbol == null
                           select t).ToList();
            }

            // and update them from the Ethplorer api
            HttpClient http = new HttpClient();
            foreach (var t in missing)
            {
                DateTime started = DateTime.Now;
                try
                {
                    var zt = await EthplorerAPI.GetTokenInfo(http, t.Address);
                    if (zt == null)
                    {
                        Debug.WriteLine($"{t.Address} null return");
                    }
                    else
                    {
                        // a new context each time to allow for recovery after connection failure
                        using (var db = new zeromevContext())
                        {
                            db.ZmTokens.Update(zt);
                            await db.SaveChangesAsync();
                        }
                        Debug.WriteLine($"{t.Address} {zt.Name} {zt.Symbol} {zt.Decimals}");
                    }
                }
                catch (HttpRequestException e)
                {
                    // probably doesn't exist- update the symbol to 'unknown' so we don't keep asking for it
                    ZmToken zt = new ZmToken();
                    zt.Address = t.Address;
                    zt.Symbol = "???";
                    using (var db = new zeromevContext())
                    {
                        db.ZmTokens.Update(zt);
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{t.Address} errored: " + e.ToString());
                }

                // don't hammer the api
                TimeSpan duration = DateTime.Now - started;
                TimeSpan delay = new TimeSpan(0, 0, 0) - duration;
                if (delay.Ticks > 0)
                    await Task.Delay(delay);
            }
        }
    }
}