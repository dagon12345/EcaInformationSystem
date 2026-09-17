using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationGranteeRowEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Birthdate",
                table: "ApplicationGranteeRows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IneligibilityReason",
                table: "ApplicationGranteeRows",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEligible",
                table: "ApplicationGranteeRows",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "ApplicationGranteeRows",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Birthdate",
                table: "ApplicationGranteeRows");

            migrationBuilder.DropColumn(
                name: "IneligibilityReason",
                table: "ApplicationGranteeRows");

            migrationBuilder.DropColumn(
                name: "IsEligible",
                table: "ApplicationGranteeRows");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "ApplicationGranteeRows");
        }
    }
}
