using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DownloadData.Data;
using DownloadData.Interfaces;
using DownloadData.Repositories;
using DownloadData.Services;
using YahooQuotesApi;
using NodaTime;

namespace DownloadData.Installers
{
    public sealed class CustomServicesInstaller : IServiceInstaller
    {
        public void InstallService(IServiceCollection services, IConfiguration configuration, ILoggingBuilder? logging = null)
        {
            services.AddDbContext<StockContext>(options => options.UseNpgsql(configuration.GetConnectionString("PostgresConnection")));
            services.AddHttpClient();
            services.AddLogging()
                    .AddScoped<CompanyDataRepository>()
                    .AddScoped<CompanyDataService>()
                    .AddScoped<HistoricalDataRepository>()
                    .AddScoped<HistoricalDataService>()
                    .AddSingleton(_ =>
                    {
                        YahooQuotesBuilder builder = new();
                        builder.WithHistoryStartDate(Instant.FromUtc(2020, 1, 1, 0, 0));
                        return builder.Build();
                    })
                    .AddScoped<YahooService>();
        }
    }
}