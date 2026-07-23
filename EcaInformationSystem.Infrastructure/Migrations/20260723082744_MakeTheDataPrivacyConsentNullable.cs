using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeTheDataPrivacyConsentNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "DataPrivacyConsent",
                table: "BeneficiaryInformations",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");
            // ✅ Null out existing records — they never actually answered this
            migrationBuilder.Sql(@"
                UPDATE BeneficiaryInformations SET DataPrivacyConsent = NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "DataPrivacyConsent",
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
