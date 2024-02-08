using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DownloadData.Data;
using DownloadData.Interfaces;
using DownloadData.Repositories;
using DownloadData.Services;

namespace DownloadData.Installers
{
    public sealed class CustomServicesInstaller : IServiceInstaller
    {
        public void InstallService(IServiceCollection services, IConfiguration configuration, ILoggingBuilder? logging = null)
        {
            _ = services.AddDbContext<StockContext>(options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
            _ = services.AddHttpClient("defaultClient", x =>
            {
                x.DefaultRequestHeaders.Add("Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                x.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            _ = services.AddLogging()
                    .AddScoped<CompanyDataRepository>()
                    .AddScoped<CompanyDataService>()
                    .AddScoped<HistoricalDataRepository>()
                    .AddScoped<HistoricalDataService>();
        }
    }
}