using System.Collections;
using System.Text;

namespace M5.BitArraySerialization
{
    internal static class BitArrayExtensions
    {
        public static string BitArrayToString(this System.Collections.BitArray bits)
        {
            StringBuilder sb = new StringBuilder(bits.Length);
            for (int i = 0; i < bits.Length; i++)
                sb.Append(bits[i] ? "1" : "0");
            return sb.ToString();
        }
    }
}