using Cocona;
using DownloadData.Enums;

namespace DownloadData.Models.Arguments
{
    public sealed class HistoricalDataArgs : ICommandParameterSet
    {
        [Option('s', Description = "The start date for the historical data.")]
        [HasDefaultValue]
        public DateOnly? StartDate { get; set; }
        [Option('e', Description = "The end date for the historical data.")]
        [HasDefaultValue]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        [Option('t', Description = "The type of historical data to download.")]
        [HasDefaultValue]
        public HistoricalType HistoricalType { get; set; } = HistoricalType.Day;
        [Option('j', Description = "The maximum number of parallel downloads.")]
        [HasDefaultValue]
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;
    }
}