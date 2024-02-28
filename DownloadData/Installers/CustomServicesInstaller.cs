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
            services.AddDbContext<StockContext>(options => options.UseNpgsql(configuration.GetConnectionString("PostgresConnection")));
            services.AddHttpClient();
            services.AddLogging()
                    .AddScoped<CompanyDataRepository>()
                    .AddScoped<CompanyDataService>()
                    .AddScoped<HistoricalDataRepository>()
                    .AddScoped<HistoricalDataService>();
        }
    }
}