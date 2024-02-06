﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DownloadData.Data;

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
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("DownloadData.Entities.Company", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Cnpj")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("CvmCode")
                        .HasColumnType("int");

                    b.Property<bool>("HasBdrs")
                        .HasColumnType("bit");

                    b.Property<bool>("HasEmissions")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

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
                        .HasColumnType("int");

                    b.Property<int>("IndustryId")
                        .HasColumnType("int");

                    b.HasKey("CompanyId", "IndustryId");

                    b.HasIndex("IndustryId");

                    b.ToTable("CompanyIndustries", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Dividend", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("int");

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("DATE");

                    b.Property<double>("ClosePrice")
                        .HasColumnType("float");

                    b.Property<string>("DividendType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<double>("DividendsPercentage")
                        .HasColumnType("float");

                    b.Property<double>("DividendsValue")
                        .HasColumnType("float");

                    b.Property<DateTime>("PriorExDate")
                        .HasColumnType("DATE");

                    b.HasKey("TickerId", "ApprovalDate");

                    b.ToTable("Dividends", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.HistoricalData", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("int");

                    b.Property<DateTime>("Date")
                        .HasColumnType("DATE");

                    b.Property<double>("Average")
                        .HasColumnType("float");

                    b.Property<double>("Close")
                        .HasColumnType("float");

                    b.Property<DateTime>("Expiration")
                        .HasColumnType("DATE");

                    b.Property<double>("High")
                        .HasColumnType("float");

                    b.Property<double>("Low")
                        .HasColumnType("float");

                    b.Property<double>("Open")
                        .HasColumnType("float");

                    b.Property<double>("Strike")
                        .HasColumnType("float");

                    b.HasKey("TickerId", "Date");

                    b.ToTable("HistoricalData", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Industry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Industries", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Split", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("int");

                    b.Property<DateTime>("LastDate")
                        .HasColumnType("DATE");

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("DATE");

                    b.Property<double>("SplitFactor")
                        .HasColumnType("float");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("VARCHAR(max)");

                    b.HasKey("TickerId", "LastDate");

                    b.ToTable("Splits", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Subscription", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("int");

                    b.Property<DateTime>("LastDate")
                        .HasColumnType("DATE");

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("DATE");

                    b.Property<double>("Percentage")
                        .HasColumnType("float");

                    b.Property<double>("PriceUnit")
                        .HasColumnType("float");

                    b.HasKey("TickerId", "LastDate");

                    b.ToTable("Subscriptions", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Ticker", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("CompanyId")
                        .HasColumnType("int");

                    b.Property<string>("Isin")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("StockTicker")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

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

            modelBuilder.Entity("DownloadData.Entities.Dividend", b =>
                {
                    b.HasOne("DownloadData.Entities.Ticker", "Ticker")
                        .WithMany("Dividends")
                        .HasForeignKey("TickerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Ticker");
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

            modelBuilder.Entity("DownloadData.Entities.Split", b =>
                {
                    b.HasOne("DownloadData.Entities.Ticker", "Ticker")
                        .WithMany("Splits")
                        .HasForeignKey("TickerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Ticker");
                });

            modelBuilder.Entity("DownloadData.Entities.Subscription", b =>
                {
                    b.HasOne("DownloadData.Entities.Ticker", "Ticker")
                        .WithMany("Subscriptions")
                        .HasForeignKey("TickerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Ticker");
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

            modelBuilder.Entity("DownloadData.Entities.Ticker", b =>
                {
                    b.Navigation("Dividends");

                    b.Navigation("HistoricalData");

                    b.Navigation("Splits");

                    b.Navigation("Subscriptions");
                });
#pragma warning restore 612, 618
        }
    }
}
