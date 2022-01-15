using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace ZeroMev.Shared
{
    public static class Num
    {
        private const long DivBeforeValueHex = 10000;
        private const long DivAfterValueHex = 100000000000000;
        private const long DivAfterGasPriceHex = 1000000000;

        private const long DivBeforeValue = 100000000000;
        private const long DivAfterValue = 10000000;

        public static bool IsValidHex(string hex)
        {
            BigInteger r;
            string hexb = ParseHex(hex);
            return BigInteger.TryParse(hexb, System.Globalization.NumberStyles.HexNumber, null, out r);
        }

        public static string ParseHex(string hex)
        {
            return hex.Substring(2).Insert(0, "00");
        }

        public static string ParseHexNo0x(string hex)
        {
            return hex.Insert(0, "00");
        }

        public static string ShortenHex(string hex, int toLength)
        {
            if (hex == null) return "";
            if (toLength > hex.Length) return hex;
            return hex.Substring(0, toLength) + "...";
        }

        public static string LongToHex(long value)
        {
            return $"0x{value:X}";
        }

        public static int HexToInt(string hex)
        {
            return int.Parse(ParseHex(hex), System.Globalization.NumberStyles.HexNumber);
        }

        public static long HexToLong(string hex)
        {
            return long.Parse(ParseHex(hex), System.Globalization.NumberStyles.HexNumber);
        }

        public static decimal HexToDec(string hex)
        {
            return decimal.Parse(ParseHex(hex), System.Globalization.NumberStyles.HexNumber);
        }

        public static string HexToDecStr(string hex)
        {
            if (hex == null) return "";
            return HexToDec(hex).ToString();
        }

        public static BigInteger HexToBigInt(string hex)
        {
            return BigInteger.Parse(ParseHex(hex), System.Globalization.NumberStyles.HexNumber);
        }

        public static string HexToBigIntStr(string hex)
        {
            if (hex == null) return "";
            return HexToBigInt(hex).ToString();
        }

        public static string HexToValue(string hex)
        {
            try
            {
                // quick and dirty conversion without requiring a BigDecimal class
                if (hex == null) return "";
                BigInteger bi = HexToBigInt(hex);
                decimal d = (decimal)(bi / DivBeforeValueHex);
                d /= DivAfterValueHex;
                return d.ToString();
            }
            catch (Exception ex)
            {
                // in case of overflow
                return "err";
            }
        }

        public static string HexToGasPrice(string hex)
        {
            try
            {
                // quick and dirty conversion without requiring a BigDecimal class
                if (hex == null) return "";
                BigInteger bi = HexToBigInt(hex);
                decimal d = (decimal)(bi);
                d /= DivAfterGasPriceHex;
                return d.ToString();
            }
            catch (Exception ex)
            {
                // in case of overflow
                return "err";
            }
        }

        public static string BigIntToGasPrice(BigInteger bi)
        {
            try
            {
                // quick and dirty conversion without requiring a BigDecimal class
                decimal d = (decimal)(bi);
                d /= DivAfterGasPriceHex;
                return d.ToString();
            }
            catch (Exception ex)
            {
                // in case of overflow
                return "err";
            }
        }

        public static string EthToGwei(string bigInt)
        {
            if (bigInt == null) return "";
            BigInteger bi = BigInteger.Parse(bigInt);
            if (bi == 0) return "0";
            decimal d = (decimal)(bi / DivBeforeValue);
            d /= DivAfterValue;
            return d.ToString();
        }
    }
}