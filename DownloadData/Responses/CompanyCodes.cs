using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses
{
    public sealed class CompanyCodes
    {
        [JsonPropertyName("code")]
        public required string Code { get; set; }
        [JsonPropertyName("isin")]
        public required string Isin { get; set; }
    }
}