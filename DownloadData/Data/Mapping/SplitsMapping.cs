using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.DownloadData.Entities;

namespace Tcc.DownloadData.Data.Mapping
{
    public sealed class SplitsMapping : IEntityTypeConfiguration<Split>
    {
        public void Configure(EntityTypeBuilder<Split> builder)
        {
            builder.ToTable("Splits");
            builder.HasKey(s => new { s.TickerId, s.LastDate });
            builder.Property(s => s.TickerId).IsRequired();
            builder.Property(s => s.LastDate).HasColumnType("DATE").IsRequired();
            builder.Property(s => s.SplitFactor).IsRequired();
            builder.Property(s => s.ApprovalDate).HasColumnType("DATE").IsRequired();
            builder.Property(s => s.Type).IsRequired();
            builder.HasOne(s => s.Ticker).WithMany(t => t.Splits).HasForeignKey(s => s.TickerId);
        }
    }
}