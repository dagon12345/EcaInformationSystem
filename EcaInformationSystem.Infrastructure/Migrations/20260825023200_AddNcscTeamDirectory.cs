using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNcscTeamDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NcscTeamDirectoryEntryId",
                table: "Logs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NcscTeamDirectoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PsgcCodeRegion = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nickname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NcscTeamDirectoryEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Log_NcscTeamDirectoryEntryId",
                table: "Logs",
                column: "NcscTeamDirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_NcscTeamDirectory_Region",
                table: "NcscTeamDirectoryEntries",
                columns: new[] { "IsDeleted", "PsgcCodeRegion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NcscTeamDirectoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_Log_NcscTeamDirectoryEntryId",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "NcscTeamDirectoryEntryId",
                table: "Logs");
        }
    }
}
