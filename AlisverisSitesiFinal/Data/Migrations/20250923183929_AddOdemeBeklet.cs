using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    public partial class AddOdemeBeklet : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OdemeBekletler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    MerchantOid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),

                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UserAddress = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),

                    // ÖNEMLİ: hassasiyet sabit
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),

                    // Sepet snapshot’ı (JSON)
                    SepetJson = table.Column<string>(type: "nvarchar(max)", nullable: false),

                    Olusturma = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdemeBekletler", x => x.Id);
                });

            // Hızlı erişim ve tekrar oluşumları önlemek için benzersiz index
            migrationBuilder.CreateIndex(
                name: "IX_OdemeBekletler_MerchantOid",
                table: "OdemeBekletler",
                column: "MerchantOid",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OdemeBekletler");
        }
    }
}
