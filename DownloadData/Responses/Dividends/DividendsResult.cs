using System.Text.Json.Serialization;
using DownloadData.Converters;

namespace DownloadData.Responses.Dividends
{
    public sealed class DividendsResult
    {
        [JsonPropertyName("closingPricePriorExDate"), JsonConverter(typeof(DoubleConverter))]
        public double ClosePrice { get; set; }
        [JsonPropertyName("corporateAction")]
        public required string DividendType { get; set; }
        [JsonPropertyName("corporateActionPrice"), JsonConverter(typeof(DoubleConverter))]
        public double DividendsPercentage { get; set; }
        [JsonPropertyName("valueCash"), JsonConverter(typeof(DoubleConverter))]
        public double DividendsValue { get; set; }
        [JsonPropertyName("dateApproval"), JsonConverter(typeof(DateConverter))]
        public DateOnly? ApprovalDate { get; set; }
        [JsonPropertyName("lastDatePriorEx"), JsonConverter(typeof(DateConverter))]
        public DateOnly? PriorExDate { get; set; }
        [JsonPropertyName("typeStock")]
        public required string StockType { get; set; }
        public string StockTicker { get; set; } = string.Empty;
    }
}