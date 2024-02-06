namespace DownloadData.Entities
{
    public sealed class Industry
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public ICollection<CompanyIndustry>? Companies { get; }
    }
}