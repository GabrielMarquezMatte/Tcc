using Cocona;
using Microsoft.Extensions.Configuration;
using DownloadData.Commands;
using DownloadData.Extensions;
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
            app.AddCommands<StockDataCommand>();
            return app.RunAsync(app.Lifetime.ApplicationStopping);
        }
    }
}