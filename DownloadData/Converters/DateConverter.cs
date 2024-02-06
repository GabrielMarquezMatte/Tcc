using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownloadData.Converters
{
    public sealed class DateConverter : JsonConverter<DateTime>
    {
        private static readonly CultureInfo Culture = CultureInfo.CreateSpecificCulture("pt-BR");
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => DateTime.TryParseExact(reader.GetString(), "dd/MM/yyyy", Culture, DateTimeStyles.AssumeLocal, out DateTime result) ? result : throw new JsonException($"Invalid date value: {reader.GetString()}"),
            _ => throw new JsonException($"Invalid date value: {reader.GetString()}"),
        };
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("dd/MM/yyyy", Culture));
        }
    }
}