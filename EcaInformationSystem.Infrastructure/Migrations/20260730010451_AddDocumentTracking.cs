using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PsgcCodeProvince = table.Column<int>(type: "int", nullable: false),
                    PsgcCodeMunicipality = table.Column<int>(type: "int", nullable: false),
                    MilestoneYear = table.Column<int>(type: "int", nullable: false),
                    DateReceived = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    CurrentHolderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentLegAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentGranteeRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    HasFinding = table.Column<bool>(type: "bit", nullable: false),
                    FindingNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FindingSetAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FindingSetByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FindingResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGranteeRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentGranteeRows_DocumentBatches_DocumentBatchId",
                        column: x => x.DocumentBatchId,
                        principalTable: "DocumentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelayedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTransfers_DocumentBatches_DocumentBatchId",
                        column: x => x.DocumentBatchId,
                        principalTable: "DocumentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBatches_PsgcCodeProvince_PsgcCodeMunicipality_MilestoneYear",
                table: "DocumentBatches",
                columns: new[] { "PsgcCodeProvince", "PsgcCodeMunicipality", "MilestoneYear" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGranteeRows_DocumentBatchId",
                table: "DocumentGranteeRows",
                column: "DocumentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTransfers_DocumentBatchId",
                table: "DocumentTransfers",
                column: "DocumentBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentGranteeRows");

            migrationBuilder.DropTable(
                name: "DocumentTransfers");

            migrationBuilder.DropTable(
                name: "DocumentBatches");
        }
    }
}
