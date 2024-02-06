using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DownloadData.Entities;

namespace DownloadData.Data.Mapping
{
    public sealed class HistoricalDataMapping : IEntityTypeConfiguration<HistoricalData>
    {
        public void Configure(EntityTypeBuilder<HistoricalData> builder)
        {
            builder.ToTable("HistoricalData");
            builder.HasKey(h => new { h.TickerId, h.Date });
            builder.Property(h => h.Date).HasColumnType("DATE").IsRequired();
            builder.Property(h => h.Open).IsRequired();
            builder.Property(h => h.High).IsRequired();
            builder.Property(h => h.Low).IsRequired();
            builder.Property(h => h.Close).IsRequired();
            builder.Property(h => h.Expiration).HasColumnType("DATE").IsRequired();
            builder.HasOne(h => h.Ticker).WithMany(t => t.HistoricalData).HasForeignKey(h => h.TickerId);
        }
    }
}