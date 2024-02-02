using System.Text.Json.Serialization;
using Tcc.DownloadData.Enums;

namespace Tcc.DownloadData.Responses.SplitSubscription
{
    public sealed class SplitsResponse
    {
        [JsonPropertyName("assetIssued")]
        public required string AssetIssued { get; set; }
        [JsonPropertyName("lastDatePrior")]
        public DateTime LastDate { get; set; }
        [JsonPropertyName("factor")]
        public double SplitFactor { get; set; }
        [JsonPropertyName("approvedOn")]
        public DateTime ApprovalDate { get; set; }
        [JsonPropertyName("label")]
        public string Type { get; set; } = string.Empty;
    }
}