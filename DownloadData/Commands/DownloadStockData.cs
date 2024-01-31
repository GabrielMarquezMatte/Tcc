using Cocona;
using Tcc.DownloadData.Models.Arguments;
using Tcc.DownloadData.Services;

namespace Tcc.DownloadData.Commands
{
    public sealed class DownloadStockData(CompanyDataService companyDataService, HistoricalDataService historicalDataService)
    {
        [Command("company-data", Description = "Download company data from the internet and save it to the database.")]
        public Task ExecuteAsync([Option('j')] int maxParallelism = 10, [Ignore] CancellationToken cancellationToken = default)
        {
            return companyDataService.SaveCompaniesAsync(maxParallelism, cancellationToken);
        }
        [Command("historical-data", Description = "Download historical data from the internet and save it to the database.")]
        public Task ExecuteAsync(HistoricalDataArgs args, [Ignore] CancellationToken cancellationToken)
        {
            return historicalDataService.ProcessFilesAsync(args.StartDate, args.EndDate, args.HistoricalType, args.MaxParallelism, cancellationToken);
        }
    }
}