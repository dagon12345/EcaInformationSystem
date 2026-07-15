using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiaryPaymentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentPaymentHistoryId",
                table: "BeneficiaryInformations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeneficiaryPaymentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollQuarter = table.Column<int>(type: "int", nullable: true),
                    FiscalYear = table.Column<int>(type: "int", nullable: true),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    ModeOfPayment = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryPaymentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryPaymentHistories_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInformation_CurrentPaymentHistoryId",
                table: "BeneficiaryInformations",
                column: "CurrentPaymentHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_Beneficiary_PaymentDate",
                table: "BeneficiaryPaymentHistories",
                columns: new[] { "BeneficiaryInformationId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_FiscalYear_Quarter_Status",
                table: "BeneficiaryPaymentHistories",
                columns: new[] { "FiscalYear", "PayrollQuarter", "PaymentStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeneficiaryPaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_BeneficiaryInformation_CurrentPaymentHistoryId",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "CurrentPaymentHistoryId",
                table: "BeneficiaryInformations");
        }
    }
}
