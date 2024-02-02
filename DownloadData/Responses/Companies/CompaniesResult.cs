using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses.Companies
{
    public sealed class CompaniesResult
    {
        [JsonPropertyName("codeCVM")]
        public int CodeCvm { get; set; }
    }
}