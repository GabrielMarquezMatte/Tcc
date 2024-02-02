using System.Text.Json.Serialization;
using Tcc.DownloadData.Responses.SplitSubscription;

namespace Tcc.DownloadData.Responses
{
    public sealed class CompanyResponse
    {
        [JsonPropertyName("cnpj")]
        public required string Cnpj { get; set; }
        [JsonPropertyName("codeCVM")]
        public int CodeCvm { get; set; }
        [JsonPropertyName("companyName")]
        public required string CompanyName { get; set; }
        [JsonPropertyName("issuingCompany")]
        public required string IssuingCompany { get; set; }
        [JsonPropertyName("industryClassification")]
        public required string IndustryClassification { get; set; }
        [JsonPropertyName("hasBDR")]
        public bool HasBdrs { get; set; }
        [JsonPropertyName("hasEmissions")]
        public bool HasEmissions { get; set; }
        [JsonPropertyName("otherCodes")]
        public IEnumerable<CompanyCodes>? OtherCodes { get; set; }
        public SplitSubscriptionResponse? SplitSubscription { get; set; }
    }
}