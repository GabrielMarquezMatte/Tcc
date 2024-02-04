namespace Tcc.DownloadData.Models.Options
{
    public sealed class DownloadUrlsOptions
    {
        public required Uri CompanyDetails { get; set; }
        public required Uri ListCompanies { get; set; }
        public required Uri HistoricalData { get; set; }
        public required Uri SplitSubscription { get; set; }
        public required Uri Dividends { get; set; }
    }
}