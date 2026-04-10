using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModifiedBeneficiaryInformationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Citizenship",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CivilStatus",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndigenousPeople",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPersonWithDisability",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemarkCategory",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Citizenship",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "CivilStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "IsIndigenousPeople",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "IsPersonWithDisability",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "RemarkCategory",
                table: "BeneficiaryInformations");
        }
    }
}
