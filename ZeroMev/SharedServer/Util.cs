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
                if (p > 0.0001 || p < -0.0001)
                    Debug.WriteLine("inaccurate");
#endif
            }
            return sb.ToString();
        }

        public static void RoundArray(ZMDecimal[] values, int decimals)
        {
            for (int i = 0; i < values.Length; i++)
                values[i] = values[i].RoundAwayFromZero(decimals);
        }

        public static Dictionary<TKey, TElement> ToSafeDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            if (source == null)
                throw new ArgumentException("source");
            if (keySelector == null)
                throw new ArgumentException("keySelector");
            if (elementSelector == null)
                throw new ArgumentException("elementSelector");
            Dictionary<TKey, TElement> d = new Dictionary<TKey, TElement>(comparer);
            foreach (TSource element in source)
            {
                if (!d.ContainsKey(keySelector(element)))
                    d.Add(keySelector(element), elementSelector(element));
            }
            return d;
        }
    }
}