namespace Tcc.DownloadData.Responses
{
    public sealed class HistoricalDataResponse
    {
        public DateTime Date { get; set; }
        public required string Ticker { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Average { get; set; }
        public double Close { get; set; }
        public double Strike { get; set; }
        public DateTime Expiration { get; set; }
    }
}