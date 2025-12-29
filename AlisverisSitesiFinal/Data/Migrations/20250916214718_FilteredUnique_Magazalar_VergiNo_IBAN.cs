using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilteredUnique_Magazalar_VergiNo_IBAN : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Magazalar_IBAN",
                table: "Magazalar",
                column: "IBAN",
                unique: true,
                filter: "[IBAN] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Magazalar_VergiNo",
                table: "Magazalar",
                column: "VergiNo",
                unique: true,
                filter: "[VergiNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Magazalar_IBAN",
                table: "Magazalar");

            migrationBuilder.DropIndex(
                name: "IX_Magazalar_VergiNo",
                table: "Magazalar");
        }
    }
}
