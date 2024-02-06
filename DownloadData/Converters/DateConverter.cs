using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownloadData.Converters
{
    public sealed class DateConverter : JsonConverter<DateTime>
    {
        private static readonly CultureInfo Culture = CultureInfo.CreateSpecificCulture("pt-BR");
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if(string.IsNullOrEmpty(value))
            {
                return DateTime.MinValue;
            }
            return reader.TokenType switch
            {
                JsonTokenType.String => DateTime.ParseExact(value, "dd/MM/yyyy", Culture, DateTimeStyles.AssumeLocal),
                _ => throw new JsonException($"Invalid date value: {value}"),
            };
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("dd/MM/yyyy", Culture));
        }
    }
}