using Cocona;
using Microsoft.Extensions.Configuration;
using Tcc.DownloadData.Commands;
using Tcc.DownloadData.Extensions;
namespace Tcc.DownloadData
{
    public static class Program
    {
        public static Task Main(string[] args)
        {
            var builder = CoconaApp.CreateBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Services.InstallServices(builder.Configuration, builder.Logging, typeof(Program).Assembly);
            var app = builder.Build();
            app.AddCommands<DownloadStockData>();
            return app.RunAsync(app.Lifetime.ApplicationStopping);
        }
    }
}