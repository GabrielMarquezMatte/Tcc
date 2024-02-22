using System.ComponentModel.DataAnnotations;

namespace DownloadData.Entities
{
    public sealed class Ticker
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        [Required, MaxLength(12)]
        public required string Isin { get; set; }
        [Required, MaxLength(10)]
        public required string StockTicker { get; set; }
        public Company? Company { get; set; }
        public ICollection<HistoricalData> HistoricalData { get; } = [];
        public ICollection<Subscription> Subscriptions { get; } = [];
        public ICollection<Split> Splits { get; } = [];
        public ICollection<Dividend> Dividends { get; } = [];
    }
}