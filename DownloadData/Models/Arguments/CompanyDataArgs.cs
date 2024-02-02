using Cocona;

namespace Tcc.DownloadData.Models.Arguments
{
    public sealed class CompanyDataArgs : ICommandParameterSet
    {
        [Option('j', Description = "Maximum parallelism for downloading company data.")]
        [HasDefaultValue]
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;
        [Option('s', Description = "Download splits data.")]
        [HasDefaultValue]
        public bool Splits { get; set; }
        [Option('m', Description = "Maximum number of splits/subscription data to download.")]
        [HasDefaultValue]
        public int MaxSplits { get; set; } = int.MaxValue;
        [Option('c', Description = "Companies to download split/subscription data.")]
        [HasDefaultValue]
        public IEnumerable<string> Companies { get; } = [];
    }
}