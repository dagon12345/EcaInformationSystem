using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiaryDuplicateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuplicateOfId",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "HasKnownDuplicate",
                table: "BeneficiaryInformations");

            migrationBuilder.CreateTable(
                name: "BeneficiaryDuplicateHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuplicateOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryDuplicateHistories", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeneficiaryDuplicateHistories");

            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateOfId",
                table: "BeneficiaryInformations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasKnownDuplicate",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
