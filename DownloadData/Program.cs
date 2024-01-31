using Cocona;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tcc.DownloadData.Commands;
using Tcc.DownloadData.Enums;
using Tcc.DownloadData.Extensions;
using Tcc.DownloadData.Models.Arguments;
using Tcc.DownloadData.Services;
namespace Tcc.DownloadData
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = CoconaApp.CreateBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Services.InstallServices(builder.Configuration, builder.Logging, typeof(Program).Assembly);
            var app = builder.Build();
            app.AddCommands<DownloadStockData>();
            var scope = app.Services.CreateAsyncScope();
            await using(scope.ConfigureAwait(false))
            {
                var historicalDataService = scope.ServiceProvider.GetRequiredService<HistoricalDataService>();
                var companyDataService = scope.ServiceProvider.GetRequiredService<CompanyDataService>();
                DownloadStockData downloadStockData = new(companyDataService, historicalDataService);
                HistoricalDataArgs historicalDataArgs = new()
                {
                    HistoricalType = HistoricalType.Year,
                    MaxParallelism = 1,
                    EndDate = new(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    StartDate = new(2005, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                };
                await downloadStockData.ExecuteAsync(historicalDataArgs, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
            }
            await app.RunAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
    }
}