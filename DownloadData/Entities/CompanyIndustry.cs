namespace DownloadData.Entities
{
    public sealed class CompanyIndustry
    {
        public int IndustryId { get; set; }
        public int CompanyId { get; set; }
        public Industry? Industry { get; set; }
        public Company? Company { get; set; }
    }
}