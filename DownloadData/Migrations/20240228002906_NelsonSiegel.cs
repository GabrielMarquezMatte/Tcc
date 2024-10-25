﻿//<auto-generated />
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DownloadData.Migrations
{
    /// <inheritdoc />
    public partial class NelsonSiegel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dividends");

            migrationBuilder.DropTable(
                name: "Splits");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.CreateTable(
                name: "NelsonSiegel",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Beta0 = table.Column<double>(type: "double precision", nullable: false),
                    Beta1 = table.Column<double>(type: "double precision", nullable: false),
                    Beta2 = table.Column<double>(type: "double precision", nullable: false),
                    Tau = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NelsonSiegel", x => x.Date);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NelsonSiegel");

            migrationBuilder.CreateTable(
                name: "Dividends",
                columns: table => new
                {
                    TickerId = table.Column<int>(type: "integer", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "DATE", nullable: false),
                    ClosePrice = table.Column<double>(type: "double precision", nullable: false),
                    DividendType = table.Column<string>(type: "text", nullable: false),
                    DividendsPercentage = table.Column<double>(type: "double precision", nullable: false),
                    DividendsValue = table.Column<double>(type: "double precision", nullable: false),
                    PriorExDate = table.Column<DateOnly>(type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dividends", x => new { x.TickerId, x.ApprovalDate });
                    table.ForeignKey(
                        name: "FK_Dividends_Tickers_TickerId",
                        column: x => x.TickerId,
                        principalTable: "Tickers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Splits",
                columns: table => new
                {
                    TickerId = table.Column<int>(type: "integer", nullable: false),
                    LastDate = table.Column<DateOnly>(type: "DATE", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "DATE", nullable: false),
                    SplitFactor = table.Column<double>(type: "double precision", nullable: false),
                    Type = table.Column<string>(type: "VARCHAR", maxLength: 13, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Splits", x => new { x.TickerId, x.LastDate });
                    table.ForeignKey(
                        name: "FK_Splits_Tickers_TickerId",
                        column: x => x.TickerId,
                        principalTable: "Tickers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    TickerId = table.Column<int>(type: "integer", nullable: false),
                    LastDate = table.Column<DateOnly>(type: "DATE", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "DATE", nullable: false),
                    Percentage = table.Column<double>(type: "double precision", nullable: false),
                    PriceUnit = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => new { x.TickerId, x.LastDate });
                    table.ForeignKey(
                        name: "FK_Subscriptions_Tickers_TickerId",
                        column: x => x.TickerId,
                        principalTable: "Tickers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
