using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Requests
{
    public sealed class CompanyRequest
    {
        [JsonPropertyName("codeCVM")]
        public int CodeCvm { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; } = "pt-br";
    }
}