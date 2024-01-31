using System.Diagnostics;
using Cocona;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Models.Arguments;
using Tcc.DownloadData.Services;

namespace Tcc.DownloadData.Commands
{
    public sealed class DownloadStockData(CompanyDataService companyDataService, HistoricalDataService historicalDataService, ILogger<DownloadStockData> logger)
    {
        private static readonly Action<ILogger, TimeSpan, Exception?> _commandTime = LoggerMessage.Define<TimeSpan>(
            LogLevel.Information,
            new EventId(1, "CommandTime"),
            "Command executed in {Time}");
        [Command("company-data", Description = "Download company data from the internet and save it to the database.")]
        public async Task ExecuteAsync([Option('j')] int maxParallelism = 10, [Ignore] CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            await companyDataService.SaveCompaniesAsync(maxParallelism, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _commandTime(logger, stopwatch.Elapsed, null);
        }
        [Command("historical-data", Description = "Download historical data from the internet and save it to the database.")]
        public async Task ExecuteAsync(HistoricalDataArgs args, [Ignore] CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            await historicalDataService.ProcessFilesAsync(args.StartDate, args.EndDate, args.HistoricalType, args.MaxParallelism, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _commandTime(logger, stopwatch.Elapsed, null);
        }
    }
}