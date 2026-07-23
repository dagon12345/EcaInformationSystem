using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLivenessVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfLiveness",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLivenessVerified",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_LivenessVerified",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "IsLivenessVerified" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_LivenessVerified",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DateOfLiveness",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "IsLivenessVerified",
                table: "BeneficiaryInformations");
        }
    }
}
