using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using System.Collections;
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
    }
}