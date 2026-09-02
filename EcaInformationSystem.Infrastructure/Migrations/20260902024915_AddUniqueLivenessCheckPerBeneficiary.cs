using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueLivenessCheckPerBeneficiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LivenessCheckRecords_BeneficiaryInformationId_IsActive",
                table: "LivenessCheckRecords");

            migrationBuilder.CreateIndex(
                name: "IX_LivenessCheckRecords_BeneficiaryInformationId",
                table: "LivenessCheckRecords",
                column: "BeneficiaryInformationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LivenessCheckRecords_BeneficiaryInformationId",
                table: "LivenessCheckRecords");

            migrationBuilder.CreateIndex(
                name: "IX_LivenessCheckRecords_BeneficiaryInformationId_IsActive",
                table: "LivenessCheckRecords",
                columns: new[] { "BeneficiaryInformationId", "IsActive" });
        }
    }
}
