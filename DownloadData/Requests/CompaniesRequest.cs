using System.Text.Json.Serialization;

namespace Tcc.DownloadData.Requests
{
    public sealed class CompaniesRequest
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "pt-br";
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; } = 1;
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 120;
    }
}