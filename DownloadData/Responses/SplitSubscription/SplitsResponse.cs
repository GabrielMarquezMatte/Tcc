using System.Text.Json.Serialization;
using DownloadData.Converters;

namespace DownloadData.Responses.SplitSubscription
{
    public sealed class SplitsResponse
    {
        [JsonPropertyName("assetIssued")]
        public required string AssetIssued { get; set; }
        [JsonPropertyName("lastDatePrior"), JsonConverter(typeof(DateConverter))]
        public DateOnly LastDate { get; set; }
        [JsonPropertyName("factor"), JsonConverter(typeof(DoubleConverter))]
        public double SplitFactor { get; set; }
        [JsonPropertyName("approvedOn"), JsonConverter(typeof(DateConverter))]
        public DateOnly ApprovalDate { get; set; }
        [JsonPropertyName("label")]
        public string Type { get; set; } = string.Empty;
    }
}