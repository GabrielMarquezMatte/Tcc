﻿// <auto-generated />
using System;
using DownloadData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DownloadData.Migrations
{
    [DbContext(typeof(StockContext))]
    [Migration("20240215032245_Modifications")]
    partial class Modifications
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
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

            modelBuilder.Entity("DownloadData.Entities.Dividend", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("DATE");

                    b.Property<double>("ClosePrice")
                        .HasColumnType("double precision");

                    b.Property<string>("DividendType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<double>("DividendsPercentage")
                        .HasColumnType("double precision");

                    b.Property<double>("DividendsValue")
                        .HasColumnType("double precision");

                    b.Property<DateTime>("PriorExDate")
                        .HasColumnType("DATE");

                    b.HasKey("TickerId", "ApprovalDate");

                    b.ToTable("Dividends", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.HistoricalData", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("Date")
                        .HasColumnType("DATE");

                    b.Property<double>("Average")
                        .HasColumnType("double precision");

                    b.Property<double>("Close")
                        .HasColumnType("double precision");

                    b.Property<DateTime>("Expiration")
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

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Industries", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Split", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("LastDate")
                        .HasColumnType("DATE");

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("DATE");

                    b.Property<double>("SplitFactor")
                        .HasColumnType("double precision");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(13)
                        .HasColumnType("VARCHAR");

                    b.HasKey("TickerId", "LastDate");

                    b.ToTable("Splits", (string)null);
                });

            modelBuilder.Entity("DownloadData.Entities.Subscription", b =>
                {
                    b.Property<int>("TickerId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("LastDate")
                        .HasColumnType("DATE");

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("DATE");

                    b.Property<double>("Percentage")
                        .HasColumnType("double precision");

                    b.Property<double>("PriceUnit")
                        .HasColumnType("double precision");

                    b.HasKey("TickerId", "LastDate");

                    b.ToTable("Subscriptions", (string)null);
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