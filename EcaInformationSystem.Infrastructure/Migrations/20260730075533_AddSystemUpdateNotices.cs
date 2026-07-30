using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemUpdateNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemUpdateNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemUpdateNotices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemUpdateNotices_PublishedAt",
                table: "SystemUpdateNotices",
                column: "PublishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemUpdateNotices");
        }
    }
}
