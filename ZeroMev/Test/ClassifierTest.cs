using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using ZeroMev.ClassifierService;
using ZeroMev.MevEFC;

namespace ZeroMev.Test
{
    [TestClass]
    public class ClassifierTest
    {
        [TestMethod]
        public async Task ClassifyTest()
        {
            using (var db = new zeromevContext())
            {
                await Classifier.ClassifyMEV(13377043, db);
            }
        }

        [TestMethod]
        public async Task BuildDEXsTest()
        {
            using (var db = new zeromevContext())
            {
                var swaps = from s in db.Swaps
                            where s.BlockNumber >= 13358564 && s.BlockNumber <= 13359564
                            orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                            select s;

                DEXs dexs = new DEXs();
                int mins = 0;
                foreach (var swap in swaps)
                {
                    dexs.Add(swap, DateTime.Now.AddMinutes(mins++));
                    //Console.WriteLine(JsonSerializer.Serialize(swap));
                }
            }
        }
    }
}