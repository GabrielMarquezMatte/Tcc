using System.Reflection;
using Microsoft.EntityFrameworkCore;
using DownloadData.Entities;

namespace DownloadData.Data
{
    public sealed class StockContext(DbContextOptions<StockContext> options) : DbContext(options)
    {
        public DbSet<Company> Companies { get; set; }
        public DbSet<Ticker> Tickers { get; set; }
        public DbSet<Industry> Industries { get; set; }
        public DbSet<CompanyIndustry> CompanyIndustries { get; set; }
        public DbSet<HistoricalData> HistoricalData { get; set; }
        public DbSet<HistoricalDataYahoo> HistoricalDataYahoos { get; set; }
        public DbSet<NelsonSiegel> NelsonSiegel { get; set; }
        private static bool CheckInterfaces(Type type)
        {
            var interfaces = type.GetInterfaces();
            return Array.Exists(interfaces, i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>));
        }
        private static bool CheckParameters(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return Array.Exists(parameters, p => p.ParameterType.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>));
        }
        private static IEnumerable<object> GetEntityTypeConfigurations()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            var configurations = types.Where(CheckInterfaces)
                                      .Select(Activator.CreateInstance);
            ArgumentNullException.ThrowIfNull(configurations);
            return configurations!;
        }
        private static void ApplyConfigurations(ModelBuilder modelBuilder)
        {
            var applyConfigurationMethod = typeof(ModelBuilder).GetMethods()
                                                               .First(m => string.Equals(m.Name, nameof(ModelBuilder.ApplyConfiguration), StringComparison.Ordinal)
                                                                           && CheckParameters(m));

            foreach (var configuration in GetEntityTypeConfigurations())
            {
                var configurationType = configuration.GetType();
                var entityType = configurationType.GetInterfaces()
                                                  .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                                                  .GetGenericArguments()[0];
                var applyConfigGenericMethod = applyConfigurationMethod.MakeGenericMethod(entityType);
                applyConfigGenericMethod.Invoke(modelBuilder, [configuration]);
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ApplyConfigurations(modelBuilder);
        }
    }
}