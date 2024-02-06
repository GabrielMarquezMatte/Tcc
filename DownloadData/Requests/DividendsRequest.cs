using System.Text.Json.Serialization;

namespace DownloadData.Requests
{
    public sealed class DividendsRequest
    {
        [JsonPropertyName("tradingName")]
        public required string TradingName { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; } = "pt-br";
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; } = -1;
        [JsonPropertyName("pageSize")]
        public int PageSize { get; } = 20;
    }
}