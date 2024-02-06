using System.Text.Json.Serialization;

namespace DownloadData.Responses.Dividends
{
    public sealed class DividendsResponse
    {
        [JsonPropertyName("results")]
        public IEnumerable<DividendsResult> Dividends { get; set; } = [];
    }
}