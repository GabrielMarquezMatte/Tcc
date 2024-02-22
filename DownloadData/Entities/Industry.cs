using System.ComponentModel.DataAnnotations;

namespace DownloadData.Entities
{
    public sealed class Industry
    {
        public int Id { get; set; }
        [Required, MaxLength(40)]
        public required string Name { get; set; }
        public ICollection<CompanyIndustry> Companies { get; } = [];
    }
}