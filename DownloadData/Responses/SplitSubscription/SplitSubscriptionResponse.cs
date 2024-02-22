using System.Text.Json.Serialization;

namespace DownloadData.Responses.SplitSubscription
{
    public sealed class SplitSubscriptionResponse
    {
        [JsonPropertyName("numberPreferredShares")]
        public long PreferredStockShares { get; set; }
        [JsonPropertyName("numberCommonShares")]
        public long CommonStockShares { get; set; }
        [JsonPropertyName("stockDividends")]
        public IEnumerable<SplitsResponse> Splits { get; set; } = [];
        [JsonPropertyName("subscriptions")]
        public IEnumerable<SubscriptionResponse> Subscriptions { get; set; } = [];
    }
}