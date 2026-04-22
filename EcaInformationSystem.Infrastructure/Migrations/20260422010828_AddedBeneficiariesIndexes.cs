using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedBeneficiariesIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OscaIdNumber",
                table: "BeneficiaryInformations",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MiddleName",
                table: "BeneficiaryInformations",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "BeneficiaryInformations",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "BeneficiaryInformations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_BeneficiaryInformationId",
                table: "Logs",
                column: "BeneficiaryInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_BeneficiaryInformationId_CreatedAt",
                table: "Logs",
                columns: new[] { "BeneficiaryInformationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Barangay",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Barangay" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_BirthDate",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "BirthDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_LastName_FirstName_MiddleName",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "LastName", "FirstName", "MiddleName" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Municipality",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Municipality" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Province",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Province" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Region",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Sex",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Sex" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_LastName_FirstName_MiddleName_BirthDate_OscaIdNumber_NcscRrn",
                table: "BeneficiaryInformations",
                columns: new[] { "LastName", "FirstName", "MiddleName", "BirthDate", "OscaIdNumber", "NcscRrn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Logs_BeneficiaryInformationId",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_Logs_BeneficiaryInformationId_CreatedAt",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Barangay",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_BirthDate",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_LastName_FirstName_MiddleName",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Municipality",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Province",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Region",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Sex",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_LastName_FirstName_MiddleName_BirthDate_OscaIdNumber_NcscRrn",
                table: "BeneficiaryInformations");

            migrationBuilder.AlterColumn<string>(
                name: "OscaIdNumber",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MiddleName",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
