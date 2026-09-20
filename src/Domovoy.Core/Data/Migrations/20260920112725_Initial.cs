using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Domovoy.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    House = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FiasId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SearchText = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagingOrganizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Inn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmergencyPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagingOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProblemCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClarifyingQuestion = table.Column<string>(type: "text", nullable: true),
                    IsEmergency = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResponsibilityZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActionHint = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponsibilityZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxUserId = table.Column<long>(type: "bigint", nullable: false),
                    BuildingId = table.Column<int>(type: "integer", nullable: true),
                    CategoryCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaxUserId = table.Column<long>(type: "bigint", nullable: false),
                    MaxChatId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    ManagingOrganizationId = table.Column<int>(type: "integer", nullable: true),
                    ManagementKind = table.Column<int>(type: "integer", nullable: false),
                    BuildYear = table.Column<int>(type: "integer", nullable: true),
                    Floors = table.Column<int>(type: "integer", nullable: true),
                    Entrances = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Buildings_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Buildings_ManagingOrganizations_ManagingOrganizationId",
                        column: x => x.ManagingOrganizationId,
                        principalTable: "ManagingOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NormativeDeadlines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NormativeDeadlines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NormativeDeadlines_ProblemCategories_ProblemCategoryId",
                        column: x => x.ProblemCategoryId,
                        principalTable: "ProblemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryResponsibilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemCategoryId = table.Column<int>(type: "integer", nullable: false),
                    ResponsibilityZoneId = table.Column<int>(type: "integer", nullable: false),
                    AppliesToManagement = table.Column<int>(type: "integer", nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    ActualAt = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryResponsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryResponsibilities_ProblemCategories_ProblemCategoryId",
                        column: x => x.ProblemCategoryId,
                        principalTable: "ProblemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryResponsibilities_ResponsibilityZones_Responsibility~",
                        column: x => x.ResponsibilityZoneId,
                        principalTable: "ResponsibilityZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppUserId = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogStates_Users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBuildingLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppUserId = table.Column<int>(type: "integer", nullable: false),
                    BuildingId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Apartment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBuildingLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBuildingLinks_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBuildingLinks_Users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SearchText",
                table: "Addresses",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_AddressId",
                table: "Buildings",
                column: "AddressId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_ManagingOrganizationId",
                table: "Buildings",
                column: "ManagingOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryResponsibilities_ProblemCategoryId",
                table: "CategoryResponsibilities",
                column: "ProblemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryResponsibilities_ResponsibilityZoneId",
                table: "CategoryResponsibilities",
                column: "ResponsibilityZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogStates_AppUserId",
                table: "DialogStates",
                column: "AppUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NormativeDeadlines_ProblemCategoryId",
                table: "NormativeDeadlines",
                column: "ProblemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemCategories_Code",
                table: "ProblemCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResponsibilityZones_Code",
                table: "ResponsibilityZones",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryEvents_Name_OccurredAt",
                table: "TelemetryEvents",
                columns: new[] { "Name", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBuildingLinks_AppUserId_BuildingId",
                table: "UserBuildingLinks",
                columns: new[] { "AppUserId", "BuildingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBuildingLinks_BuildingId",
                table: "UserBuildingLinks",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_MaxUserId",
                table: "Users",
                column: "MaxUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryResponsibilities");

            migrationBuilder.DropTable(
                name: "DialogStates");

            migrationBuilder.DropTable(
                name: "NormativeDeadlines");

            migrationBuilder.DropTable(
                name: "TelemetryEvents");

            migrationBuilder.DropTable(
                name: "UserBuildingLinks");

            migrationBuilder.DropTable(
                name: "ResponsibilityZones");

            migrationBuilder.DropTable(
                name: "ProblemCategories");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "ManagingOrganizations");
        }
    }
}
