using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTCKimlikToAdres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TCKimlik",
                table: "Adresler",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TCKimlik",
                table: "Adresler");
        }
    }
}
