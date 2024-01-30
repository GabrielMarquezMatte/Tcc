using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.DownloadData.Entities;

namespace Tcc.DownloadData.Data.Mapping
{
    public sealed class TickersMapping : IEntityTypeConfiguration<Ticker>
    {
        public void Configure(EntityTypeBuilder<Ticker> builder)
        {
            builder.ToTable("Tickers");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Isin).IsRequired();
            builder.Property(t => t.StockTicker).IsRequired();
            builder.HasOne(t => t.Company).WithMany(c => c.Tickers).HasForeignKey(t => t.CompanyId);
            builder.HasIndex(t => t.StockTicker).IsUnique();
        }
    }
}