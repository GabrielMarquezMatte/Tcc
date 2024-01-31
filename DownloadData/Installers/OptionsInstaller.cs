using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Interfaces;
using Tcc.DownloadData.Models.Options;

namespace Tcc.DownloadData.Installers
{
    public sealed class OptionsInstaller : IServiceInstaller
    {
        public void InstallService(IServiceCollection services, IConfiguration configuration, ILoggingBuilder? logging = null)
        {
            services.AddOptions<DownloadUrlsOptions>()
                .Bind(configuration.GetRequiredSection("DownloadUrls"));
        }
    }
}