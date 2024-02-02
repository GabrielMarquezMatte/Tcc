namespace Tcc.DownloadData.Entities
{
    public sealed class Subscription
    {
        public int TickerId { get; set; }
        public DateTime LastDate { get; set; }
        public double Percentage { get; set; }
        public double PriceUnit { get; set; }
        public DateTime ApprovalDate { get; set; }
        public Ticker? Ticker { get; set; }
    }
}