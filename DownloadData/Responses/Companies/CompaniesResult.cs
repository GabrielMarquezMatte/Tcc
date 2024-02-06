using System.Text.Json.Serialization;

namespace DownloadData.Responses.Companies
{
    public sealed class CompaniesResult
    {
        [JsonPropertyName("codeCVM")]
        public int CodeCvm { get; set; }
    }
}