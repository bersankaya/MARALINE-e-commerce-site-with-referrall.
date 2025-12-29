using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIadeTalebi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) TcKimlikNo indeksini kaldır
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TcKimlikNo",
                table: "AspNetUsers");

            // 2) TcKimlikNo kolonunu (EF’in istediği gibi) değiştir
            // Not: Buradaki type/nullability modeline göre EF üretmişti; senin hedefin buysa kalsın.
            migrationBuilder.AlterColumn<string>(
                name: "TcKimlikNo",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // 3) Filtreli unique index (NULL değerleri yok sayar)
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TcKimlikNo",
                table: "AspNetUsers",
                column: "TcKimlikNo",
                unique: true,
                filter: "[TcKimlikNo] IS NOT NULL");

            // 4) IadeTalebi tablosu
            migrationBuilder.CreateTable(
                name: "IadeTalepleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                         .Annotation("SqlServer:Identity", "1, 1"),
                    SiparisId = table.Column<int>(type: "int", nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(50)", nullable: false, defaultValue: "Beklemede"),
                    Aciklama = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TalepTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SonucTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReddetmeNedeni = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IadeTalepleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IadeTalepleri_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IadeTalepleri_SiparisId",
                table: "IadeTalepleri",
                column: "SiparisId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // IadeTalebi tablosunu geri al
            migrationBuilder.DropTable(
                name: "IadeTalepleri");

            // Filtreli indexi kaldır
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TcKimlikNo",
                table: "AspNetUsers");

            // Kolonu eski haline döndür (EF’in eski şemasına göre)
            migrationBuilder.AlterColumn<string>(
                name: "TcKimlikNo",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            // Eski (filtre-siz) indexi geri getir (gerekliyse)
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TcKimlikNo",
                table: "AspNetUsers",
                column: "TcKimlikNo",
                unique: true);
        }
    }
}
