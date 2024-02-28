namespace DownloadData.Entities
{
    public sealed class HistoricalDataYahoo
    {
        public DateOnly Date { get; set; }
        public int TickerId { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Adjusted { get; set; }
        public long Volume { get; set; }
        public Ticker? Ticker { get; set; }
    }
}