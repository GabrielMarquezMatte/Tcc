namespace DownloadData.Entities
{
    public sealed class Split
    {
        public int TickerId { get; set; }
        public DateTime LastDate { get; set; }
        public double SplitFactor { get; set; }
        public DateTime ApprovalDate { get; set; }
        public string Type { get; set; } = string.Empty;
        public Ticker? Ticker { get; set; }
    }
}