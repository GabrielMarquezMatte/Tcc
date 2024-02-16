namespace DownloadData.Entities
{
    public sealed class Subscription
    {
        public int TickerId { get; set; }
        public DateOnly LastDate { get; set; }
        public double Percentage { get; set; }
        public double PriceUnit { get; set; }
        public DateOnly ApprovalDate { get; set; }
        public Ticker? Ticker { get; set; }
    }
}