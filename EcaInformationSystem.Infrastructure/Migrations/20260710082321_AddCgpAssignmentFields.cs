using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCgpAssignmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CgpPageNumber",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CgpPrefix",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CgpPageNumber",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "CgpPrefix",
                table: "BeneficiaryInformations");
        }
    }
}
