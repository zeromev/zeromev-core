using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public static class Util
    {
        public static string DisplayArray(ZMDecimal[] values, string varName, int decimals)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
                sb.AppendLine($"{varName}{i} = {values[i].RoundAwayFromZero(decimals)}");
            return sb.ToString();
        }

        public static string DisplayArrayAB(ZMDecimal[] a, ZMDecimal[] b, int decimals)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < a.Length; i++)
                sb.AppendLine($"{a[i].RoundAwayFromZero(decimals)}\t{b[i].RoundAwayFromZero(decimals)}");
            return sb.ToString();
        }

        public static string DisplayCompareAB(string aLabel, string bLabel, ZMDecimal[] a, ZMDecimal[] b, int decimals, bool doShowZeroPercent = true, int? toIndex = null)
        {
            StringBuilder sb = new StringBuilder();

            if (toIndex == null)
                toIndex = a.Length;

            sb.AppendLine($"{aLabel}\t{bLabel}\terror %");
            for (int i = 0; i < toIndex; i++)
            {
                var p = 1 - (a[i] / b[i]);
                if (doShowZeroPercent || p > 0.00000001 || p < -0.00000001)
                    sb.AppendLine($"{a[i].RoundAwayFromZero(decimals)}\t{b[i].RoundAwayFromZero(decimals)}\t{((decimal)p).ToString("P")}");
#if (DEBUG)
                //if (p > 0.001 || p < -0.001)
                //Debug.WriteLine("inaccurate");
#endif
            }
            return sb.ToString();
        }

        public static void RoundArray(ZMDecimal[] values, int decimals)
        {
            for (int i = 0; i < values.Length; i++)
                values[i] = values[i].RoundAwayFromZero(decimals);
        }
    }
}