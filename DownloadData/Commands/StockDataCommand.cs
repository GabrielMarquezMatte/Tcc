using System.Diagnostics;
using Cocona;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Models.Arguments;
using Tcc.DownloadData.Services;

namespace Tcc.DownloadData.Commands
{
    public sealed class StockDataCommand(CompanyDataService companyDataService, HistoricalDataService historicalDataService, ILogger<StockDataCommand> logger)
    {
        private static readonly Action<ILogger, TimeSpan, Exception?> _commandTime = LoggerMessage.Define<TimeSpan>(
            LogLevel.Information,
            new EventId(1, "CommandTime"),
            "Command executed in {Time}");
        private static async Task ExecuteCommandAsync(Func<CancellationToken, Task> command, ILogger logger, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            await command(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _commandTime(logger, stopwatch.Elapsed, arg3: null);
        }
        [Command("company-data", Description = "Download company data from the internet and save it to the database.")]
        public Task ExecuteAsync([Option('j')] int maxParallelism = 10, [Ignore] CancellationToken cancellationToken = default)
        {
            return ExecuteCommandAsync(
                token => companyDataService.SaveCompaniesAsync(maxParallelism, token),
                logger,
                cancellationToken);
        }
        [Command("historical-data", Description = "Download historical data from the internet and save it to the database.")]
        public Task ExecuteAsync(HistoricalDataArgs args, [Ignore] CancellationToken cancellationToken)
        {
            return ExecuteCommandAsync(
                token => historicalDataService.ProcessFilesAsync(args.StartDate, args.EndDate, args.HistoricalType, args.MaxParallelism, token),
                logger,
                cancellationToken);
        }
    }
}