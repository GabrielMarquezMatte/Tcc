using System.ComponentModel.DataAnnotations;

namespace DownloadData.Entities
{
    public sealed class Split
    {
        public int TickerId { get; set; }
        public DateOnly LastDate { get; set; }
        public double SplitFactor { get; set; }
        public DateOnly ApprovalDate { get; set; }
        [MaxLength(13)]
        public string Type { get; set; } = string.Empty;
        public Ticker? Ticker { get; set; }
    }
}