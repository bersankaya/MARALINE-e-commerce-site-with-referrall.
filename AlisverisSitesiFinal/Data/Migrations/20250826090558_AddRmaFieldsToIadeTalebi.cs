using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRmaFieldsToIadeTalebi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IadeAdres",
                table: "IadeTalepleri",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IadeTalimat",
                table: "IadeTalepleri",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusteriKargoFirmasi",
                table: "IadeTalepleri",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusteriKargoTakipNo",
                table: "IadeTalepleri",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RmaKodu",
                table: "IadeTalepleri",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RmaOlusturmaTarihi",
                table: "IadeTalepleri",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IadeAdres",
                table: "IadeTalepleri");

            migrationBuilder.DropColumn(
                name: "IadeTalimat",
                table: "IadeTalepleri");

            migrationBuilder.DropColumn(
                name: "MusteriKargoFirmasi",
                table: "IadeTalepleri");

            migrationBuilder.DropColumn(
                name: "MusteriKargoTakipNo",
                table: "IadeTalepleri");

            migrationBuilder.DropColumn(
                name: "RmaKodu",
                table: "IadeTalepleri");

            migrationBuilder.DropColumn(
                name: "RmaOlusturmaTarihi",
                table: "IadeTalepleri");
        }
    }
}
