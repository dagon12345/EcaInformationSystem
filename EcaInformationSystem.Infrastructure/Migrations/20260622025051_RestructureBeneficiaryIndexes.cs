using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureBeneficiaryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Barangay",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Batch",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_BirthDate",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Municipality",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Province",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_RefYear",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Region",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Sex",
                table: "BeneficiaryInformations");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_LastName_FirstName_MiddleName_BirthDate_OscaIdNumber_NcscRrn",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_DuplicateDetection");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter_Batch_RefYear",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_RefNumber");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_LastName_FirstName_MiddleName",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_NameSort_Default");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_IsEligible",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_IsEligible");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_IsCompliant",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_IsCompliant");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsEligible",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_CoStatus_Eligible");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsCompliant",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_CoStatus_Compliant");

            migrationBuilder.RenameIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_CoStatus",
                table: "BeneficiaryInformations",
                newName: "IX_Beneficiary_CoStatus");

            migrationBuilder.AlterColumn<string>(
                name: "BatchCode",
                table: "BeneficiaryInformations",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_Barangay_NameSort",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Barangay", "LastName", "FirstName", "MiddleName" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_BatchCode_NameSort",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "BatchCode", "LastName", "FirstName", "MiddleName" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_BirthDate_NameSort",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "BirthDate", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_Municipality_NameSort",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Municipality", "LastName", "FirstName", "MiddleName" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_PaymentStatus",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_Province_NameSort",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Province", "LastName", "FirstName", "MiddleName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_Barangay_NameSort",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_BatchCode_NameSort",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_BirthDate_NameSort",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_Municipality_NameSort",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_PaymentStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_Province_NameSort",
                table: "BeneficiaryInformations");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_RefNumber",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_Quarter_Batch_RefYear");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_NameSort_Default",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_LastName_FirstName_MiddleName");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_IsEligible",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_IsEligible");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_IsCompliant",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_IsCompliant");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_DuplicateDetection",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_LastName_FirstName_MiddleName_BirthDate_OscaIdNumber_NcscRrn");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_CoStatus_Eligible",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsEligible");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_CoStatus_Compliant",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_CoStatus_IsCompliant");

            migrationBuilder.RenameIndex(
                name: "IX_Beneficiary_CoStatus",
                table: "BeneficiaryInformations",
                newName: "IX_BeneficiaryInformations_IsDeleted_CoStatus");

            migrationBuilder.AlterColumn<string>(
                name: "BatchCode",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Barangay",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Barangay" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Batch",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Batch" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_BirthDate",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "BirthDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Municipality",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Municipality" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Province",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Province" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_RefYear",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "RefYear" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Region",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Sex",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Sex" });
        }
    }
}
