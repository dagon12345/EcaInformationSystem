using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ColumnPhoneNumberAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhoneNumber",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "BeneficiaryInformations");
        }
    }
}
