using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses.SplitSubscription
{
    public sealed class SplitSubscriptionResponse
    {
        [JsonPropertyName("stockDividends")]
        public IEnumerable<SplitsResponse>? Splits { get; set; }
        [JsonPropertyName("subscriptions")]
        public IEnumerable<SubscriptionResponse>? Subscriptions { get; set; }
    }
}