using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Responses
{
    public sealed class CompaniesResult
    {
        [JsonPropertyName("codeCVM")]
        public int CodeCvm { get; set; }
    }
}