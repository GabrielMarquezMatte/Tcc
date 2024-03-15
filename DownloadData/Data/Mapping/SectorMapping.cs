using DownloadData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DownloadData.Data.Mapping
{
    public sealed class SectorMapping : IEntityTypeConfiguration<Sector>
    {
        private static readonly Sector[] Sectors = [
            new (){ Id = 1, Name = "Varejo" },
            new (){ Id = 2, Name = "Alimentos" },
            new (){ Id = 3, Name = "Commodities" },
            new (){ Id = 4, Name = "Indústria" },
            new (){ Id = 5, Name = "Energia" },
            new (){ Id = 6, Name = "Financeiro" },
            new (){ Id = 7, Name = "Telecomunicações" },
            new (){ Id = 8, Name = "Saúde" },
            new (){ Id = 9, Name = "Transporte" },
            new (){ Id = 10, Name = "Utilidade Pública" },
            new (){ Id = 11, Name = "Imobiliário" },
            new (){ Id = 12, Name = "Outros" },
        ];
        public void Configure(EntityTypeBuilder<Sector> builder)
        {
            builder.ToTable("Sector");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Name).HasColumnType("VARCHAR").HasMaxLength(30).IsRequired();
            builder.HasMany(s => s.Industries).WithOne(i => i.Sector).HasForeignKey(i => i.SectorId);
            builder.HasData(Sectors);
        }
    }
}