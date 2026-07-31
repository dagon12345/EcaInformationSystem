using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyAccomplishmentReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DarReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreparedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreparedByPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NotedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NotedByPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UseSharedEssentialFunctions = table.Column<bool>(type: "bit", nullable: false),
                    SharedEssentialFunctions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DarReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DarEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DarReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EssentialFunctionsOverride = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AccomplishmentText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsWorkFromHome = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DarEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DarEntries_DarReports_DarReportId",
                        column: x => x.DarReportId,
                        principalTable: "DarReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DarEntry_DarReportId",
                table: "DarEntries",
                column: "DarReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DarReport_UserId_PeriodStart",
                table: "DarReports",
                columns: new[] { "UserId", "PeriodStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DarEntries");

            migrationBuilder.DropTable(
                name: "DarReports");
        }
    }
}
