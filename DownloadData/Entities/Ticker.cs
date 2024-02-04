namespace Tcc.DownloadData.Entities
{
    public sealed class Ticker
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public required string Isin { get; set; }
        public required string StockTicker { get; set; }
        public Company? Company { get; set; }
        public ICollection<HistoricalData>? HistoricalData { get; }
        public ICollection<Subscription>? Subscriptions { get; }
        public ICollection<Split>? Splits { get; }
        public ICollection<Dividend>? Dividends { get; }
    }
}