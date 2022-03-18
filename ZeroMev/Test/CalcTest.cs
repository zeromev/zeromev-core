using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Diagnostics;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.Test
{
    [TestClass]
    public class CalcTest
    {
        [TestMethod]
        public void TestZMDecimalExtensions()
        {
            Pow(2, 2);
            Pow(4, 2);
            Pow(8, 2);
            Pow(2.22222, 2);
        }

        private void Pow(ZMDecimal a, uint y)
        {
            Debug.WriteLine($"{a} ^ {y} {a.Pow(y)}");
        }

        [TestMethod]
        public void TestTimezones()
        {
            // verify timezone conversion behaviour
            DateTime now = DateTime.Now;
            DateTime nowUtc = DateTime.Now.ToUniversalTime();
            Assert.AreNotEqual(nowUtc, now);
            Assert.AreNotEqual(nowUtc.Ticks, now.Ticks);
            Debug.WriteLine($"{now} {now.Kind} {nowUtc} {nowUtc.Kind}");

            // DateTime.Now.Ticks should be different from DateTime.Now.ToUniversalTime().Ticks
            Assert.AreNotEqual(now.Ticks, DateTime.Now.ToUniversalTime().Ticks);

            // new DateTime(DateTime.Now.ToUniversalTime().Ticks, DateTimeKind.Utc) should be the same as the original local DateTime.Now (but with a different Kind)
            DateTime roundtrip = new DateTime(now.ToUniversalTime().Ticks, DateTimeKind.Utc);
            Assert.AreEqual(roundtrip, now);
            Assert.AreEqual(roundtrip.Ticks, now.Ticks);
        }
    }
}