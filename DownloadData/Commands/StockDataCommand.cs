using System.Diagnostics;
using Cocona;
using Microsoft.Extensions.Logging;
using DownloadData.Models.Arguments;
using DownloadData.Services;

namespace DownloadData.Commands
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
        public Task ExecuteAsync(CompanyDataArgs companyDataArgs, [Ignore] CancellationToken cancellationToken)
        {
            return ExecuteCommandAsync(
                token => companyDataService.SaveCompaniesAsync(companyDataArgs, token),
                logger,
                cancellationToken);
        }
        [Command("historical-data", Description = "Download historical data from the internet and save it to the database.")]
        public Task ExecuteAsync(HistoricalDataArgs args, [Ignore] CancellationToken cancellationToken)
        {
            return ExecuteCommandAsync(
                token => historicalDataService.ProcessFilesAsync(args, token),
                logger,
                cancellationToken);
        }
    }
}