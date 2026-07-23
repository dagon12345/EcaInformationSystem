using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnnexA_NullableDemographics_RrnMirror_ClaimantBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsPersonWithDisability",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsIndigenousPeople",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");
                
            migrationBuilder.Sql(@"
                UPDATE BeneficiaryInformations
                SET IsPersonWithDisability = NULL, IsIndigenousPeople = NULL
            ");

            migrationBuilder.CreateTable(
                name: "BeneficiaryClaimantBankAccounts",
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
                    table.PrimaryKey("PK_BeneficiaryClaimantBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryClaimantBankAccounts_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ClaimantBankAccount_BeneficiaryInformationId",
                table: "BeneficiaryClaimantBankAccounts",
                column: "BeneficiaryInformationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeneficiaryClaimantBankAccounts");

            migrationBuilder.Sql(@"
            UPDATE BeneficiaryInformations SET IsPersonWithDisability = 0 WHERE IsPersonWithDisability IS NULL
        ");
            migrationBuilder.Sql(@"
            UPDATE BeneficiaryInformations SET IsIndigenousPeople = 0 WHERE IsIndigenousPeople IS NULL
        ");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPersonWithDisability",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsIndigenousPeople",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);
        }
    }
}
