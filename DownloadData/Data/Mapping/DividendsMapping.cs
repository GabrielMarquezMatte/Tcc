using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.DownloadData.Entities;

namespace Tcc.DownloadData.Data.Mapping
{
    public sealed class DividendsMapping : IEntityTypeConfiguration<Dividend>
    {
        public void Configure(EntityTypeBuilder<Dividend> builder)
        {
            builder.ToTable("Dividends");
            builder.HasKey(d => new { d.TickerId, d.ApprovalDate });
            builder.Property(d => d.ApprovalDate).HasColumnType("DATE").IsRequired();
            builder.Property(d => d.PriorExDate).HasColumnType("DATE").IsRequired();
            builder.Property(d => d.ClosePrice).IsRequired();
            builder.Property(d => d.DividendType).IsRequired();
            builder.Property(d => d.DividendsPercentage).IsRequired();
            builder.Property(d => d.DividendsValue).IsRequired();
            builder.HasOne(d => d.Ticker).WithMany(t => t.Dividends).HasForeignKey(d => d.TickerId);
        }
    }
}