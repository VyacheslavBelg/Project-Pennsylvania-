using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequestNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Уже поданные обращения нумеруются задним числом в порядке подачи:
            // иначе у всех разом оказался бы номер 0.
            migrationBuilder.Sql(
                """
                WITH ordered AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "AppUserId"
                               ORDER BY COALESCE("SubmittedAt", "CreatedAt"), "Id") AS n
                    FROM "Requests"
                    WHERE "Status" <> 0
                )
                UPDATE "Requests" r
                SET "Number" = ordered.n
                FROM ordered
                WHERE r."Id" = ordered."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Number",
                table: "Requests");
        }
    }
}
