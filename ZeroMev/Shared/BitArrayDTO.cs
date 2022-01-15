using System.Collections;
using System.Text.Json.Serialization;

namespace M5.BitArraySerialization.Json
{
    // use 0 and 1s instead of to make it semi-human readable as each entry is then 1 char
    internal class BitArrayDTO
    {
        public BitArrayDTO()
        {
        }

        public BitArrayDTO(BitArray ba)
        {
            B = ba.BitArrayToString();
        }

        [JsonPropertyName("b")]
        public string B { get; set; }

        public BitArray AsBitArray()
        {
            BitArray b = new BitArray(B.Length);
            for (int i = 0; i < B.Length; i++)
                b[i] = (B[i] == '1' ? true : false);
            return b; // TODO set string
        }
    }
}