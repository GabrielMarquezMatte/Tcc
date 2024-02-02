using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses.Companies
{
    public sealed class CompaniesResponse
    {
        [JsonPropertyName("page")]
        public required CompaniesPage Page { get; set; }
        [JsonPropertyName("results")]
#pragma warning disable CA2227 // Collection properties should be read only
        public ICollection<CompaniesResult>? Results { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}