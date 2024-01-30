namespace Tcc.DownloadData.Entities
{
    public sealed class Company
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Cnpj { get; set; }
        public required string Industry { get; set; }
        public bool HasBdrs { get; set; }
        public bool HasEmissions { get; set; }
        public int CvmCode { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public ICollection<Ticker>? Tickers { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}