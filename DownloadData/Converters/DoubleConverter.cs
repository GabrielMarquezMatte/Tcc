using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownloadData.Converters
{
    public sealed class DoubleConverter : JsonConverter<double>
    {
        private static readonly CultureInfo Culture = CultureInfo.CreateSpecificCulture("pt-BR");
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => double.TryParse(reader.GetString(), NumberStyles.AllowThousands | NumberStyles.Float, Culture, out double result) ? result : throw new JsonException($"Invalid double value: {reader.GetString()}"),
            JsonTokenType.Number => reader.GetDouble(),
            _ => 0,
        };
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}