using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateVerifierFieldsFromChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VerificationChecklist_DateOfVerification",
                table: "BeneficiaryVerificationChecklists");

            migrationBuilder.DropColumn(
                name: "DateOfVerification",
                table: "BeneficiaryVerificationChecklists");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                table: "BeneficiaryVerificationChecklists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfVerification",
                table: "BeneficiaryVerificationChecklists",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedBy",
                table: "BeneficiaryVerificationChecklists",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChecklist_DateOfVerification",
                table: "BeneficiaryVerificationChecklists",
                column: "DateOfVerification");
        }
    }
}
