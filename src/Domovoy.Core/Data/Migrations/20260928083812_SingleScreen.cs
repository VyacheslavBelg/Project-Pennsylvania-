using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SingleScreen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastBotMessageId",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastBotMessageId",
                table: "Users");
        }
    }
}
