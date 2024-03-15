namespace DownloadData.Entities
{
    public sealed class Sector
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public ICollection<Industry> Industries { get; } = [];
    }
}