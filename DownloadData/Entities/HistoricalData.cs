namespace DownloadData.Entities
{
    public sealed class HistoricalData
    {
        public DateTime Date { get; set; }
        public int TickerId { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Average { get; set; }
        public double Close { get; set; }
        public double Strike { get; set; }
        public DateTime Expiration { get; set; }
        public Ticker? Ticker { get; set; }
    }
}