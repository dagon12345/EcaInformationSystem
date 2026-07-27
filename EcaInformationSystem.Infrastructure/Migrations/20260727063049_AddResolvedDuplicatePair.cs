using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResolvedDuplicatePair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResolvedDuplicatePairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Record1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Record2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UnresolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnresolvedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolvedDuplicatePairs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ResolvedDuplicatePair_Records",
                table: "ResolvedDuplicatePairs",
                columns: new[] { "Record1Id", "Record2Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResolvedDuplicatePairs");
        }
    }
}
