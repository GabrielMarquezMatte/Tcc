using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses.Companies
{
    public sealed class CompaniesResponse
    {
        [JsonPropertyName("results")]
        public IEnumerable<CompaniesResult> Results { get; set; } = [];
    }
}