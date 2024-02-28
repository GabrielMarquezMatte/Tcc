using DownloadData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DownloadData.Data.Mapping
{
    public sealed class NelsonSiegelMapping : IEntityTypeConfiguration<NelsonSiegel>
    {
        public void Configure(EntityTypeBuilder<NelsonSiegel> builder)
        {
            builder.HasKey(ns => ns.Date);
            builder.Property(ns => ns.Date).IsRequired();
            builder.Property(ns => ns.Beta0).IsRequired();
            builder.Property(ns => ns.Beta1).IsRequired();
            builder.Property(ns => ns.Beta2).IsRequired();
            builder.Property(ns => ns.Tau0).IsRequired();
        }
    }
}