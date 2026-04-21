using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Added3ColumnsDateAppliedEndorsedAndAssessmentRemarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssessmentRemarks",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateApplied",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateEndorsed",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentRemarks",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DateApplied",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DateEndorsed",
                table: "BeneficiaryInformations");
        }
    }
}
