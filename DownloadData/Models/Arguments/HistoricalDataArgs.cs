using Cocona;
using Tcc.DownloadData.Enums;

namespace Tcc.DownloadData.Models.Arguments
{
    public sealed class HistoricalDataArgs : ICommandParameterSet
    {
        [Option('s', Description = "The start date for the historical data.")]
        public DateTime? StartDate { get; set; }
        [Option('e', Description = "The end date for the historical data.")]
        public DateTime? EndDate { get; set; }
        [Option('t', Description = "The type of historical data to download.")]
        [HasDefaultValue]
        public HistoricalType HistoricalType { get; set; } = HistoricalType.Day;
        [Option('j', Description = "The maximum number of parallel downloads.")]
        [HasDefaultValue]
        public int MaxParallelism { get; set; } = 10;
    }
}