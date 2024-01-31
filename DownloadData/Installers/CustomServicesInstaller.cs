using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Interfaces;
using Tcc.DownloadData.Repositories;
using Tcc.DownloadData.Services;

namespace Tcc.DownloadData.Installers
{
    public sealed class CustomServicesInstaller : IServiceInstaller
    {
        public void InstallService(IServiceCollection services, IConfiguration configuration, ILoggingBuilder? logging = null)
        {
            services.AddDbContext<StockContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });
            services.AddHttpClient("defaultClient", x =>
            {
                x.DefaultRequestHeaders.Add("Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                x.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            services.AddLogging();
            services.AddScoped<CompanyDataRepository>();
            services.AddScoped<CompanyDataService>();
            services.AddScoped<HistoricalDataRepository>();
            services.AddScoped<HistoricalDataService>();
        }
    }
}