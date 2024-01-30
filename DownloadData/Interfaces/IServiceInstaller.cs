using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tcc.DownloadData.Interfaces
{
    public interface IServiceInstaller
    {
        void InstallService(IServiceCollection services, IConfiguration configuration, ILoggingBuilder? logging = null);
    }
}