using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DownloadData.Interfaces;

namespace DownloadData.Extensions
{
    public static class DependencyInjectionExtension
    {
        public static IServiceCollection InstallServices(this IServiceCollection services, IConfiguration configuration, ILoggingBuilder? logging = null, params Assembly[] assemblies)
        {
            var serviceInstallers = assemblies.SelectMany(a => a.DefinedTypes)
                                     .Where(IsAssignableToType<IServiceInstaller>)
                                     .Select(Activator.CreateInstance).Cast<IServiceInstaller>();
            foreach (var serviceInstaller in serviceInstallers)
            {
                serviceInstaller.InstallService(services, configuration, logging);
            }
            return services;
        }
        private static bool IsAssignableToType<T>(TypeInfo typeInfo)
        {
            return typeof(T).IsAssignableFrom(typeInfo) && !typeInfo.IsInterface && !typeInfo.IsAbstract;
        }
    }
}