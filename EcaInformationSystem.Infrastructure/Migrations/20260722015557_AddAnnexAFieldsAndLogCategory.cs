using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnexAFieldsAndLogCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BeneficiaryInformationId",
                table: "Logs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // ✅ NEW — null out any Log rows pointing at a BeneficiaryInformationId that
            // no longer exists (orphaned references from historical hard deletes, prior
            // to this system's soft-delete convention). Must run before the FK is added
            // below, or the ADD CONSTRAINT step fails with error 547 exactly as seen.
            migrationBuilder.Sql(@"
                UPDATE [Logs]
                SET [BeneficiaryInformationId] = NULL
                WHERE [BeneficiaryInformationId] IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM [BeneficiaryInformations] b
                    WHERE b.[Id] = [Logs].[BeneficiaryInformationId]
                );
            ");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "Logs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Activity",
                table: "Logs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Logs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Beneficiary");

            migrationBuilder.AddColumn<string>(
                name: "CivilStatusOtherDetail",
                table: "BeneficiaryInformations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DataPrivacyConsent",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateSigned",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityType",
                table: "BeneficiaryInformations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DualCitizenshipDetails",
                table: "BeneficiaryInformations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EthnicityName",
                table: "BeneficiaryInformations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HouseNumber",
                table: "BeneficiaryInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSignedDeclaration",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlaceOfSubmission",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetName",
                table: "BeneficiaryInformations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "BeneficiaryInformations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "BeneficiaryInformations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeneficiaryAbroadAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StreetName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryAbroadAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryAbroadAddresses_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryBankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreferredChannel = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankOrWalletName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BankAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsJointAccount = table.Column<bool>(type: "bit", nullable: true),
                    SwiftCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryBankAccounts_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryClaimants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RelationshipToDeceased = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StreetName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Barangay = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CityMunicipality = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryClaimants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryClaimants_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryFamilyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationType = table.Column<int>(type: "int", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Sex = table.Column<int>(type: "int", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    IsLivingWithGrantee = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryFamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryFamilyMembers_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryVerificationChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasAnnexAForm = table.Column<bool>(type: "bit", nullable: false),
                    AnnexARemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasPrimaryIdLocal = table.Column<bool>(type: "bit", nullable: false),
                    PrimaryIdLocalRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasPrimaryIdAbroad = table.Column<bool>(type: "bit", nullable: false),
                    PrimaryIdAbroadRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasSecondaryIds = table.Column<bool>(type: "bit", nullable: false),
                    SecondaryIdsRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasPhoto = table.Column<bool>(type: "bit", nullable: false),
                    PhotoRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasBankDepositSlip = table.Column<bool>(type: "bit", nullable: false),
                    BankDepositSlipRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasDeathCertificate = table.Column<bool>(type: "bit", nullable: false),
                    DeathCertificateRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasProofOfRelationship = table.Column<bool>(type: "bit", nullable: false),
                    ProofOfRelationshipRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasClaimantBankSlip = table.Column<bool>(type: "bit", nullable: false),
                    ClaimantBankSlipRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasWarrantyReleaseForm = table.Column<bool>(type: "bit", nullable: false),
                    WarrantyReleaseFormRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasLguRcfCertification = table.Column<bool>(type: "bit", nullable: false),
                    LguRcfCertificationRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VerifierOffice = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DateOfVerification = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryVerificationChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryVerificationChecklists_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Log_Category_CreatedAt",
                table: "Logs",
                columns: new[] { "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_TrackingNumber",
                table: "BeneficiaryInformations",
                column: "TrackingNumber");

            migrationBuilder.CreateIndex(
                name: "UQ_AbroadAddress_BeneficiaryInformationId",
                table: "BeneficiaryAbroadAddresses",
                column: "BeneficiaryInformationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_BankAccount_BeneficiaryInformationId",
                table: "BeneficiaryBankAccounts",
                column: "BeneficiaryInformationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Claimant_BeneficiaryInformationId",
                table: "BeneficiaryClaimants",
                column: "BeneficiaryInformationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMember_BeneficiaryInformationId",
                table: "BeneficiaryFamilyMembers",
                column: "BeneficiaryInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChecklist_DateOfVerification",
                table: "BeneficiaryVerificationChecklists",
                column: "DateOfVerification");

            migrationBuilder.CreateIndex(
                name: "UQ_VerificationChecklist_BeneficiaryInformationId",
                table: "BeneficiaryVerificationChecklists",
                column: "BeneficiaryInformationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Logs_BeneficiaryInformations_BeneficiaryInformationId",
                table: "Logs",
                column: "BeneficiaryInformationId",
                principalTable: "BeneficiaryInformations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Logs_BeneficiaryInformations_BeneficiaryInformationId",
                table: "Logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "BeneficiaryInformationId",
                table: "Logs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid?),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Logs_BeneficiaryInformations_BeneficiaryInformationId",
                table: "Logs");

            migrationBuilder.DropTable(
                name: "BeneficiaryAbroadAddresses");

            migrationBuilder.DropTable(
                name: "BeneficiaryBankAccounts");

            migrationBuilder.DropTable(
                name: "BeneficiaryClaimants");

            migrationBuilder.DropTable(
                name: "BeneficiaryFamilyMembers");

            migrationBuilder.DropTable(
                name: "BeneficiaryVerificationChecklists");

            migrationBuilder.DropIndex(
                name: "IX_Log_Category_CreatedAt",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_TrackingNumber",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "CivilStatusOtherDetail",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DataPrivacyConsent",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DateSigned",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DisabilityType",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "DualCitizenshipDetails",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "EthnicityName",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "HouseNumber",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "IsSignedDeclaration",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "PlaceOfSubmission",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "StreetName",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "BeneficiaryInformations");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "Logs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Activity",
                table: "Logs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);
        }
    }
}
