using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DownloadData.Entities;
using CommunityToolkit.HighPerformance.Buffers;

namespace DownloadData.Data.Mapping
{
    public sealed class TickersMapping : IEntityTypeConfiguration<Ticker>
    {
        public void Configure(EntityTypeBuilder<Ticker> builder)
        {
            builder.ToTable("Tickers");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Isin).HasColumnType("VARCHAR").HasMaxLength(12).IsRequired();
            builder.Property(t => t.StockTicker)
                .HasColumnType("VARCHAR")
                .HasMaxLength(10)
                .HasConversion(x => StringPool.Shared.GetOrAdd(x), x => StringPool.Shared.GetOrAdd(x))
                .IsRequired();
            builder.HasOne(t => t.Company).WithMany(c => c.Tickers).HasForeignKey(t => t.CompanyId);
            builder.HasIndex(t => t.StockTicker).IsUnique();
        }
    }
}