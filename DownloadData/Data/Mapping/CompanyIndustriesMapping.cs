using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DownloadData.Entities;

namespace DownloadData.Data.Mapping
{
    public sealed class CompanyIndustriesMapping : IEntityTypeConfiguration<CompanyIndustry>
    {
        public void Configure(EntityTypeBuilder<CompanyIndustry> builder)
        {
            builder.ToTable("CompanyIndustries");
            builder.HasKey(ci => new { ci.CompanyId, ci.IndustryId });
            builder.HasOne(ci => ci.Company).WithMany(c => c.Industries).HasForeignKey(ci => ci.CompanyId);
            builder.HasOne(ci => ci.Industry).WithMany(i => i.Companies).HasForeignKey(ci => ci.IndustryId);
        }
    }
}