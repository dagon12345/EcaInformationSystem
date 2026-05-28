using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedIsEligibleIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_IsEligible",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "IsEligible" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_IsEligible",
                table: "BeneficiaryInformations");
        }
    }
}
