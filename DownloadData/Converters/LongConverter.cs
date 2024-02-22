using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownloadData.Converters
{
    public sealed class LongConverter : JsonConverter<long>
    {
        private static readonly CultureInfo Culture = CultureInfo.CreateSpecificCulture("pt-BR");
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => long.TryParse(reader.GetString(), NumberStyles.AllowThousands, Culture, out long result) ? result : throw new JsonException($"Invalid long value: {reader.GetString()}"),
                JsonTokenType.Number => reader.GetInt64(),
                _ => 0,
            };
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}