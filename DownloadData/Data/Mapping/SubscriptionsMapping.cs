using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.DownloadData.Entities;

namespace Tcc.DownloadData.Data.Mapping
{
    public sealed class SubscriptionsMapping : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("Subscriptions");
            builder.HasKey(s => new { s.TickerId, s.LastDate });
            builder.Property(s => s.TickerId).IsRequired();
            builder.Property(s => s.LastDate).HasColumnType("DATE").IsRequired();
            builder.Property(s => s.Percentage).IsRequired();
            builder.Property(s => s.PriceUnit).IsRequired();
            builder.Property(s => s.ApprovalDate).HasColumnType("DATE").IsRequired();
            builder.HasOne(s => s.Ticker).WithMany(t => t.Subscriptions).HasForeignKey(s => s.TickerId);
        }
    }
}