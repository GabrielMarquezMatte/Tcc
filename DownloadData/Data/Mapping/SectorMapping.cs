using DownloadData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DownloadData.Data.Mapping
{
    public sealed class SectorMapping : IEntityTypeConfiguration<Sector>
    {
        private static readonly Sector[] Sectors = [
            new (){ Id = 1, Name = "Agronegócio" },
            new (){ Id = 2, Name = "Bens Industriais" },
            new (){ Id = 3, Name = "Commodities" },
            new (){ Id = 4, Name = "Consumo cíclico" },
            new (){ Id = 5, Name = "Consumo não-cíclico" },
            new (){ Id = 6, Name = "Energia Elétrica" },
            new (){ Id = 7, Name = "Financeiro" },
            new (){ Id = 8, Name = "Imobiliário" },
            new (){ Id = 9, Name = "Industrial" },
            new (){ Id = 10, Name = "Materiais básicos" },
            new (){ Id = 11, Name = "Saúde" },
            new (){ Id = 12, Name = "Outros" },
            new (){ Id = 13, Name = "Telecomunicações" },
            new (){ Id = 14, Name = "Transporte" },
            new (){ Id = 15, Name = "Utilidade Pública" },
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