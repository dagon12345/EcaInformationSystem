using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DateAddedModeOfPaymentPropertyAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "isEligible",
                table: "BeneficiaryInformations",
                newName: "IsEligible");

            migrationBuilder.RenameColumn(
                name: "isDeleted",
                table: "BeneficiaryInformations",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "isDeceased",
                table: "BeneficiaryInformations",
                newName: "IsDeceased");

            migrationBuilder.RenameColumn(
                name: "isCompliant",
                table: "BeneficiaryInformations",
                newName: "IsCompliant");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModeOfPayment",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ModeOfPayment",
                table: "BeneficiaryInformations");

            migrationBuilder.RenameColumn(
                name: "IsEligible",
                table: "BeneficiaryInformations",
                newName: "isEligible");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "BeneficiaryInformations",
                newName: "isDeleted");

            migrationBuilder.RenameColumn(
                name: "IsDeceased",
                table: "BeneficiaryInformations",
                newName: "isDeceased");

            migrationBuilder.RenameColumn(
                name: "IsCompliant",
                table: "BeneficiaryInformations",
                newName: "isCompliant");
        }
    }
}
