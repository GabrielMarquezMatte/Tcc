using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DownloadData.Entities;
namespace DownloadData.Data.Mapping
{
    public sealed class IndustriesMapping : IEntityTypeConfiguration<Industry>
    {
        public void Configure(EntityTypeBuilder<Industry> builder)
        {
            builder.ToTable("Industries");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Name).HasColumnType("VARCHAR").HasMaxLength(40).IsRequired();
            builder.HasMany(i => i.Companies).WithOne(ci => ci.Industry).HasForeignKey(ci => ci.IndustryId);
            builder.HasIndex(i => i.Name).IsUnique();
        }
    }
}