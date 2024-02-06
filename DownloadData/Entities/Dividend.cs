namespace DownloadData.Entities
{
    public sealed class Dividend
    {
        public int TickerId { get; set; }
        public DateTime ApprovalDate { get; set; }
        public DateTime PriorExDate { get; set; }
        public double ClosePrice { get; set; }
        public required string DividendType { get; set; }
        public double DividendsPercentage { get; set; }
        public double DividendsValue { get; set; }
        public Ticker? Ticker { get; set; }
    }
}