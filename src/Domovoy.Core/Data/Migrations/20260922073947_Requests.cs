using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Domovoy.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Requests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClarifyingOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResponsibilityZoneId = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsEmergency = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClarifyingOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClarifyingOptions_ProblemCategories_ProblemCategoryId",
                        column: x => x.ProblemCategoryId,
                        principalTable: "ProblemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClarifyingOptions_ResponsibilityZones_ResponsibilityZoneId",
                        column: x => x.ResponsibilityZoneId,
                        principalTable: "ResponsibilityZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppUserId = table.Column<int>(type: "integer", nullable: false),
                    BuildingId = table.Column<int>(type: "integer", nullable: false),
                    ProblemCategoryId = table.Column<int>(type: "integer", nullable: false),
                    ClarifyingOptionId = table.Column<int>(type: "integer", nullable: true),
                    ResponsibilityZoneId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    GeneratedText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadlineDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeadlineLegalBasis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    BreachNotified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requests_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Requests_ClarifyingOptions_ClarifyingOptionId",
                        column: x => x.ClarifyingOptionId,
                        principalTable: "ClarifyingOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Requests_ProblemCategories_ProblemCategoryId",
                        column: x => x.ProblemCategoryId,
                        principalTable: "ProblemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requests_ResponsibilityZones_ResponsibilityZoneId",
                        column: x => x.ResponsibilityZoneId,
                        principalTable: "ResponsibilityZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Requests_Users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClarifyingOptions_ProblemCategoryId",
                table: "ClarifyingOptions",
                column: "ProblemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarifyingOptions_ResponsibilityZoneId",
                table: "ClarifyingOptions",
                column: "ResponsibilityZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_AppUserId",
                table: "Requests",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_BuildingId",
                table: "Requests",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ClarifyingOptionId",
                table: "Requests",
                column: "ClarifyingOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ProblemCategoryId",
                table: "Requests",
                column: "ProblemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ResponsibilityZoneId",
                table: "Requests",
                column: "ResponsibilityZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status_DeadlineAt",
                table: "Requests",
                columns: new[] { "Status", "DeadlineAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "ClarifyingOptions");
        }
    }
}
