using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollLinkFieldsToFormDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FiscalYear",
                table: "FormDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollQuarter",
                table: "FormDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PsgcCodeRegion",
                table: "FormDocuments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FiscalYear",
                table: "FormDocuments");

            migrationBuilder.DropColumn(
                name: "PayrollQuarter",
                table: "FormDocuments");

            migrationBuilder.DropColumn(
                name: "PsgcCodeRegion",
                table: "FormDocuments");
        }
    }
}
