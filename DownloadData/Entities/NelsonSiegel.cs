namespace DownloadData.Entities
{
    public sealed class NelsonSiegel
    {
        public DateOnly Date { get; set; }
        public double Beta0 { get; set; }
        public double Beta1 { get; set; }
        public double Beta2 { get; set; }
        public double Tau0 { get; set; }
    }
}