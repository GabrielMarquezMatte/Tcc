using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownloadData.Converters
{
    public sealed class DateConverter : JsonConverter<DateOnly>
    {
        private static readonly CultureInfo Culture = CultureInfo.CreateSpecificCulture("pt-BR");
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if(string.IsNullOrEmpty(value))
            {
                return DateOnly.MinValue;
            }
            return reader.TokenType switch
            {
                JsonTokenType.String => DateOnly.ParseExact(value, "dd/MM/yyyy", Culture, DateTimeStyles.AssumeLocal),
                _ => throw new JsonException($"Invalid date value: {value}"),
            };
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("dd/MM/yyyy", Culture));
        }
    }
}