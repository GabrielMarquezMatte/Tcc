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
            services.AddHttpClient<CompanyDataRepository>("CompanyClient", x =>
            {
                x.DefaultRequestHeaders.Add("Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                x.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                x.DefaultRequestHeaders.Add("Cookie", "dtCookie=v_4_srv_28_sn_F5D93CE107FFFDECF0B238E08BFEC501_perc_100000_ol_0_mul_1_app-3Afd69ce40c52bd20e_0_rcs-3Acss_0; TS0171d45d=011d592ce1079517c8b23e5c2535dd69de96df1de783aae6cebe03b889a52be62d6484725d6c0eb8467d79b38e67d179003ed58a4c; BIGipServerpool_sistemaswebb3-listados_8443_WAF=1329140746.64288.0000");
                x.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                x.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                x.DefaultRequestHeaders.Add("Host", "sistemaswebb3-listados.b3.com.br");
            });
            services.AddHttpClient<HistoricalDataRepository>("HistoricalClient", x =>
            {
                x.DefaultRequestHeaders.Add("Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
            });
            services.AddLogging()
                    .AddScoped<CompanyDataRepository>()
                    .AddScoped<CompanyDataService>()
                    .AddScoped<HistoricalDataRepository>()
                    .AddScoped<HistoricalDataService>();
        }
    }
}