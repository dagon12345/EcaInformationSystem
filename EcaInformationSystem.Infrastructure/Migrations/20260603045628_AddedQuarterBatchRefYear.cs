using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedQuarterBatchRefYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Batch",
                table: "BeneficiaryInformations",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quarter",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefYear",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Batch",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Batch" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter_Batch_RefYear",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "Quarter", "Batch", "RefYear" });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_RefYear",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "RefYear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Batch",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_Quarter_Batch_RefYear",
                table: "BeneficiaryInformations");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformations_IsDeleted_RefYear",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "Batch",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "Quarter",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "RefYear",
                table: "BeneficiaryInformations");
        }
    }
}
