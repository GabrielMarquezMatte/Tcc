using System.Text.Json.Serialization;

namespace DownloadData.Responses.Companies
{
    public sealed class CompaniesResponse
    {
        [JsonPropertyName("results")]
        public IEnumerable<CompaniesResult> Results { get; set; } = [];
    }
}