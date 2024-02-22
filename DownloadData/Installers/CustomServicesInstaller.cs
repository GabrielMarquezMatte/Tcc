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
                x.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
                x.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                x.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                x.DefaultRequestHeaders.Add("Sec-Ch-Ua", "Not A(Brand\";v=\"99\", \"Brave\";v=\"121\", \"Chromium\";v=\"121");
                x.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
                x.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "Windows");
                x.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                x.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                x.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                x.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                x.DefaultRequestHeaders.Add("Sec-Gpc", "1");
                x.DefaultRequestHeaders.Add("Cookie", "dtCookie=v_4_srv_28_sn_6E7DD36F246236B927F2B0FD52486F0B_perc_100000_ol_0_mul_1_app-3Afd69ce40c52bd20e_1_rcs-3Acss_0; TS0171d45d=011d592ce19af04c8f6dee0ca36e290927e614f47182ac73cca64f461945a37e4e5206386731d26005a6ac4de5c5b6bf6f471f8cd1; BIGipServerpool_sistemaswebb3-listados_8443_WAF=1329140746.64288.0000");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                UseCookies = true,
                UseDefaultCredentials = false,
            });
            services.AddHttpClient<HistoricalDataRepository>("HistoricalClient", x => x.DefaultRequestHeaders.Add("Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36"));
            services.AddLogging()
                    .AddScoped<CompanyDataRepository>()
                    .AddScoped<CompanyDataService>()
                    .AddScoped<HistoricalDataRepository>()
                    .AddScoped<HistoricalDataService>();
        }
    }
}