namespace DownloadData.Entities
{
    public sealed class Company
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Cnpj { get; set; }
        public bool HasBdrs { get; set; }
        public bool HasEmissions { get; set; }
        public int CvmCode { get; set; }
        public ICollection<Ticker>? Tickers { get; }
        public ICollection<CompanyIndustry>? Industries { get; }
    }
}