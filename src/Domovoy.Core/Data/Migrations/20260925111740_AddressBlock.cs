using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddressBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Block",
                table: "Addresses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Block",
                table: "Addresses");
        }
    }
}
