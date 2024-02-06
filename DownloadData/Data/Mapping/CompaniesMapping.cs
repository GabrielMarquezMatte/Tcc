using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DownloadData.Entities;

namespace DownloadData.Data.Mapping
{
    public sealed class CompaniesMapping : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.ToTable("Companies");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).IsRequired();
            builder.Property(c => c.Cnpj).IsRequired();
            builder.Property(c => c.HasBdrs).IsRequired();
            builder.Property(c => c.HasEmissions).IsRequired();
            builder.Property(c => c.CvmCode).IsRequired();
            builder.HasMany(c => c.Tickers).WithOne(t => t.Company).HasForeignKey(t => t.CompanyId);
            builder.HasIndex(c => c.CvmCode).IsUnique();
            builder.HasIndex(c => c.Cnpj).IsUnique();
        }
    }
}