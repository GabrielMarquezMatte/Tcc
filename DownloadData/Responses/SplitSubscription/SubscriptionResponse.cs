using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses.SplitSubscription
{
    public sealed class SubscriptionResponse
    {
        [JsonPropertyName("assetIssued")]
        public required string AssetIssued { get; set; }
        // The double have "," as decimal separator
        [JsonPropertyName("percentage")]
        public double Percentage { get; set; }
        [JsonPropertyName("priceUnit")]
        public double PriceUnit { get; set; }
        [JsonPropertyName("approvedOn")]
        public DateTime ApprovalDate { get; set; }
        [JsonPropertyName("lastDatePrior")]
        public DateTime LastDate { get; set; }
    }
}