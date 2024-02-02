using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Requests
{
    public sealed class SplitSubscriptionRequest
    {
        [JsonPropertyName("issuingCompany")]
        public required string IssuingCompany { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; } = "pt-br";
    }
}