using System.Text.Json.Serialization;

namespace DownloadData.Requests
{
    public sealed class CompaniesRequest
    {
        [JsonPropertyName("language")]
        public string Language { get; } = "pt-br";
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; } = -1;
        [JsonPropertyName("pageSize")]
        public int PageSize { get; } = 120;
    }
}