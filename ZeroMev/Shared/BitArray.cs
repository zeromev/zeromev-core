using M5.BitArraySerialization.Json;
using System.Collections;

namespace System.Text.Json.Serialization
{
    public class BitArrayConverter : JsonConverter<BitArray>
    {
        public override BitArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dto = JsonSerializer.Deserialize<BitArrayDTO>(ref reader, options);
            return dto.AsBitArray();
        }

        public override void Write(Utf8JsonWriter writer, BitArray value, JsonSerializerOptions options)
        {
            var dto = new BitArrayDTO(value);
            JsonSerializer.Serialize(writer, dto, options);
        }
    }
}