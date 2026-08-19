using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericDocumentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "DocumentTrackingSerialSeq");

            migrationBuilder.CreateTable(
                name: "TrackedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentHolderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentLegAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelayedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsFinding = table.Column<bool>(type: "bit", nullable: false),
                    FindingJustification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RaisedByRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRoutes_TrackedDocuments_TrackedDocumentId",
                        column: x => x.TrackedDocumentId,
                        principalTable: "TrackedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoutes_TrackedDocumentId",
                table: "DocumentRoutes",
                column: "TrackedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDocuments_SerialNumber",
                table: "TrackedDocuments",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentRoutes");

            migrationBuilder.DropTable(
                name: "TrackedDocuments");

            migrationBuilder.DropSequence(
                name: "DocumentTrackingSerialSeq");
        }
    }
}
