﻿//<autogenerated />
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DownloadData.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParametros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Beta3",
                table: "NelsonSiegel");

            migrationBuilder.DropColumn(
                name: "Tau1",
                table: "NelsonSiegel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Beta3",
                table: "NelsonSiegel",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Tau1",
                table: "NelsonSiegel",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}