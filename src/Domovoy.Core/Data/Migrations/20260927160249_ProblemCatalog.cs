using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProblemCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Addressee",
                table: "ResponsibilityZones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Requests",
                type: "text",
                nullable: true);

            // Суть уже поданных обращений восстанавливается из темы или категории,
            // иначе в списке и напоминаниях она оказалась бы пустой.
            migrationBuilder.Sql(
                """
                UPDATE "Requests" r
                SET "Subject" = COALESCE(
                    (SELECT o."Text" FROM "ClarifyingOptions" o WHERE o."Id" = r."ClarifyingOptionId"),
                    (SELECT c."Title" FROM "ProblemCategories" c WHERE c."Id" = r."ProblemCategoryId"))
                WHERE r."Subject" IS NULL;
                """);

            migrationBuilder.AddColumn<int>(
                name: "ClarifyingOptionId",
                table: "NormativeDeadlines",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NormativeDeadlines_ClarifyingOptionId",
                table: "NormativeDeadlines",
                column: "ClarifyingOptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_NormativeDeadlines_ClarifyingOptions_ClarifyingOptionId",
                table: "NormativeDeadlines",
                column: "ClarifyingOptionId",
                principalTable: "ClarifyingOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NormativeDeadlines_ClarifyingOptions_ClarifyingOptionId",
                table: "NormativeDeadlines");

            migrationBuilder.DropIndex(
                name: "IX_NormativeDeadlines_ClarifyingOptionId",
                table: "NormativeDeadlines");

            migrationBuilder.DropColumn(
                name: "Addressee",
                table: "ResponsibilityZones");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ClarifyingOptionId",
                table: "NormativeDeadlines");
        }
    }
}
