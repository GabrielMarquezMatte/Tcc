using DownloadData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DownloadData.Data.Mapping
{
    public sealed class HistoricalDataYahooMapping : IEntityTypeConfiguration<HistoricalDataYahoo>
    {
        public void Configure(EntityTypeBuilder<HistoricalDataYahoo> builder)
        {
            builder.ToTable("HistoricalDataYahoo");
            builder.HasKey(h => new { h.TickerId, h.Date });
            builder.Property(h => h.Date).HasColumnType("DATE").IsRequired();
            builder.Property(h => h.Open).IsRequired();
            builder.Property(h => h.High).IsRequired();
            builder.Property(h => h.Low).IsRequired();
            builder.Property(h => h.Close).IsRequired();
            builder.Property(h => h.Adjusted).IsRequired();
            builder.Property(h => h.Volume).IsRequired();
            builder.HasOne(h => h.Ticker).WithMany(t => t.HistoricalDataYahoos).HasForeignKey(h => h.TickerId);
        }
    }
}