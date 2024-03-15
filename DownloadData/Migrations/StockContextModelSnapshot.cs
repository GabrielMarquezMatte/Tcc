﻿// <auto-generated />
using System;
using DownloadData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DownloadData.Migrations
{
    [DbContext(typeof(StockContext))]
    partial class StockContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("DownloadData.Entities.Company", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Cnpj")
                        .IsRequired()
                        .HasMaxLength(14)
                        .HasColumnType("VARCHAR");

                    b.Property<long>("CommonStockShares")
                        .HasColumnType("bigint");

                    b.Property<int>("CvmCode")
                        .HasColumnType("integer");

                    b.Property<bool>("HasBdrs")
                        .HasColumnType("boolean");

                    b.Property<bool>("HasEmissions")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("VARCHAR");

                    b.Property<long>("PreferredStockShares")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("Cnpj")
                        .IsUnique();

                    b.HasIndex("CvmCode")
                        .IsUnique();

                    b.ToTable("Companies", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.CompanyIndustry", b =>
                {
                    b.Property<int>("CompanyId")
                        .HasColumnType("integer");

                    b.Property<int>("IndustryId")
                        .HasColumnType("integer");

                    b.HasKey("CompanyId", "IndustryId");

                    b.HasIndex("IndustryId");

                    b.ToTable("CompanyIndustries", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.HistoricalData", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("integer");

                    b.Property<DateOnly>("Date")
                        .HasColumnType("DATE");

                    b.Property<double>("Average")
                        .HasColumnType("double precision");

                    b.Property<double>("Close")
                        .HasColumnType("double precision");

                    b.Property<DateOnly>("Expiration")
                        .HasColumnType("DATE");

                    b.Property<double>("High")
                        .HasColumnType("double precision");

                    b.Property<double>("Low")
                        .HasColumnType("double precision");

                    b.Property<double>("Open")
                        .HasColumnType("double precision");

                    b.Property<double>("Strike")
                        .HasColumnType("double precision");

                    b.HasKey("TickerId", "Date");

                    b.ToTable("HistoricalData", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.HistoricalDataYahoo", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("integer");

                    b.Property<DateOnly>("Date")
                        .HasColumnType("DATE");

                    b.Property<double>("Adjusted")
                        .HasColumnType("double precision");

                    b.Property<double>("Close")
                        .HasColumnType("double precision");

                    b.Property<double>("High")
                        .HasColumnType("double precision");

                    b.Property<double>("Low")
                        .HasColumnType("double precision");

                    b.Property<double>("Open")
                        .HasColumnType("double precision");

                    b.Property<long>("Volume")
                        .HasColumnType("bigint");

                    b.HasKey("TickerId", "Date");

                    b.ToTable("HistoricalDataYahoo", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Industry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("VARCHAR");

                    b.Property<int>("SectorId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(12);

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.HasIndex("SectorId");

                    b.ToTable("Industries", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.NelsonSiegel", b =>
                {
                    b.Property<DateOnly>("Date")
                        .HasColumnType("date");

                    b.Property<double>("Beta0")
                        .HasColumnType("double precision");

                    b.Property<double>("Beta1")
                        .HasColumnType("double precision");

                    b.Property<double>("Beta2")
                        .HasColumnType("double precision");

                    b.Property<double>("Tau0")
                        .HasColumnType("double precision");

                    b.HasKey("Date");

                    b.ToTable("NelsonSiegel");
                });

            modelBuilder.Entity("DownloadData.Entities.Sector", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("VARCHAR");

                    b.HasKey("Id");

                    b.ToTable("Sector", (string)null);

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Name = "Varejo"
                        },
                        new
                        {
                            Id = 2,
                            Name = "Alimentos"
                        },
                        new
                        {
                            Id = 3,
                            Name = "Commodities"
                        },
                        new
                        {
                            Id = 4,
                            Name = "Indústria"
                        },
                        new
                        {
                            Id = 5,
                            Name = "Energia"
                        },
                        new
                        {
                            Id = 6,
                            Name = "Financeiro"
                        },
                        new
                        {
                            Id = 7,
                            Name = "Telecomunicações"
                        },
                        new
                        {
                            Id = 8,
                            Name = "Saúde"
                        },
                        new
                        {
                            Id = 9,
                            Name = "Transporte"
                        },
                        new
                        {
                            Id = 10,
                            Name = "Utilidade Pública"
                        },
                        new
                        {
                            Id = 11,
                            Name = "Imobiliário"
                        },
                        new
                        {
                            Id = 12,
                            Name = "Outros"
                        });
                });

            modelBuilder.Entity("DownloadData.Entities.Ticker", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("CompanyId")
                        .HasColumnType("integer");

                    b.Property<string>("Isin")
                        .IsRequired()
                        .HasMaxLength(12)
                        .HasColumnType("VARCHAR");

                    b.Property<string>("StockTicker")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("VARCHAR");

                    b.HasKey("Id");

                    b.HasIndex("CompanyId");

                    b.HasIndex("StockTicker")
                        .IsUnique();

                    b.ToTable("Tickers", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.CompanyIndustry", b =>
                {
                    b.HasOne("DownloadData.Entities.Company", "Company")
                        .WithMany("Industries")
                        .HasForeignKey("CompanyId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DownloadData.Entities.Industry", "Industry")
                        .WithMany("Companies")
                        .HasForeignKey("IndustryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Company");

                    b.Navigation("Industry");
                });

            modelBuilder.Entity("DownloadData.Entities.HistoricalData", b =>
                {
                    b.HasOne("DownloadData.Entities.Ticker", "Ticker")
                        .WithMany("HistoricalData")
                        .HasForeignKey("TickerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Ticker");
                });

            modelBuilder.Entity("DownloadData.Entities.HistoricalDataYahoo", b =>
                {
                    b.HasOne("DownloadData.Entities.Ticker", "Ticker")
                        .WithMany("HistoricalDataYahoos")
                        .HasForeignKey("TickerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Ticker");
                });

            modelBuilder.Entity("DownloadData.Entities.Industry", b =>
                {
                    b.HasOne("DownloadData.Entities.Sector", "Sector")
                        .WithMany("Industries")
                        .HasForeignKey("SectorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Sector");
                });

            modelBuilder.Entity("DownloadData.Entities.Ticker", b =>
                {
                    b.HasOne("DownloadData.Entities.Company", "Company")
                        .WithMany("Tickers")
                        .HasForeignKey("CompanyId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Company");
                });

            modelBuilder.Entity("DownloadData.Entities.Company", b =>
                {
                    b.Navigation("Industries");

                    b.Navigation("Tickers");
                });

            modelBuilder.Entity("DownloadData.Entities.Industry", b =>
                {
                    b.Navigation("Companies");
                });

            modelBuilder.Entity("DownloadData.Entities.Sector", b =>
                {
                    b.Navigation("Industries");
                });

            modelBuilder.Entity("DownloadData.Entities.Ticker", b =>
                {
                    b.Navigation("HistoricalData");

                    b.Navigation("HistoricalDataYahoos");
                });
#pragma warning restore 612, 618
        }
    }
}
