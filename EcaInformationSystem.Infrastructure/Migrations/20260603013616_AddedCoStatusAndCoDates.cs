using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedCoStatusAndCoDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CoDateApproved",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoDateEndorsed",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoStatus",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "CoStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsCompliant",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "CoStatus", "IsCompliant" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsEligible",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "CoStatus", "IsEligible" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsCompliant",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsEligible",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "CoDateApproved",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "CoDateEndorsed",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "CoStatus",
                table: "BeneficiaryInformations");
        }
    }
}
