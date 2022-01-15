using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using System.Collections;
using System.Threading.Tasks;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
namespace ZeroMev.Test
{
    [TestClass]
    public class BitArrayTest
    {
        [TestMethod]
        public void BitArraySerialization()
        {
            BitArray ba = new BitArray(10);
            ba.Set(3, true);
            ba.Set(5, true);
            ba.Set(7, true);

            string json = JsonSerializer.Serialize(ba, ZMSerializeOptions.Default);
            BitArray cloneba = JsonSerializer.Deserialize<BitArray>(json, ZMSerializeOptions.Default);
            Assert.AreEqual(ba.Length, cloneba.Length);
            for (int i = 0; i < ba.Length; i++)
                Assert.AreEqual(ba[i], cloneba[i]);
        }
    }
}