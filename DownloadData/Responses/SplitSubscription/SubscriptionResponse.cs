using System.Text.Json.Serialization;
using DownloadData.Converters;

namespace DownloadData.Responses.SplitSubscription
{
    public sealed class SubscriptionResponse
    {
        [JsonPropertyName("assetIssued")]
        public required string AssetIssued { get; set; }
        [JsonPropertyName("percentage"), JsonConverter(typeof(DoubleConverter))]
        public double Percentage { get; set; }
        [JsonPropertyName("priceUnit"), JsonConverter(typeof(DoubleConverter))]
        public double PriceUnit { get; set; }
        [JsonPropertyName("approvedOn"), JsonConverter(typeof(DateConverter))]
        public DateOnly ApprovalDate { get; set; }
        [JsonPropertyName("lastDatePrior"), JsonConverter(typeof(DateConverter))]
        public DateOnly LastDate { get; set; }
    }
}