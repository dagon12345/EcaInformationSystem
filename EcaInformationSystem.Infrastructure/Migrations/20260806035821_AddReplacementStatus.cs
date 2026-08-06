using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReplacementStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByBeneficiaryId",
                table: "BeneficiaryInformations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacementDate",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementRemarks",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementStatus",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesBeneficiaryId",
                table: "BeneficiaryInformations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_ReplacementStatus",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "ReplacementStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_ReplacementStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacedByBeneficiaryId",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacementDate",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacementRemarks",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacementStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacesBeneficiaryId",
                table: "BeneficiaryInformations");
        }
    }
}
