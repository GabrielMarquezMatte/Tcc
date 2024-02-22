using System.ComponentModel.DataAnnotations;

namespace DownloadData.Entities
{
    public sealed class Company
    {
        public int Id { get; set; }
        [Required, MaxLength(100)]
        public required string Name { get; set; }
        [Required, MaxLength(14)]
        public required string Cnpj { get; set; }
        public bool HasBdrs { get; set; }
        public bool HasEmissions { get; set; }
        public int CvmCode { get; set; }
        public long CommonStockShares { get; set; }
        public long PreferredStockShares { get; set; }
        public ICollection<Ticker> Tickers { get; } = [];
        public ICollection<CompanyIndustry> Industries { get; } = [];
    }
}