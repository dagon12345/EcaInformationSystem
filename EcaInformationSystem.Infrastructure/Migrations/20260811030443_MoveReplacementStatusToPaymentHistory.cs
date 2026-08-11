using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveReplacementStatusToPaymentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ Add the new columns FIRST — the data backfill below needs to read
            // from the old BeneficiaryInformations columns while writing into these,
            // so both sides must exist at the same time.
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByPaymentHistoryId",
                table: "BeneficiaryPaymentHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacementDate",
                table: "BeneficiaryPaymentHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementRemarks",
                table: "BeneficiaryPaymentHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementStatus",
                table: "BeneficiaryPaymentHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesPaymentHistoryId",
                table: "BeneficiaryPaymentHistories",
                type: "uniqueidentifier",
                nullable: true);

            // ✅ Data backfill — for every beneficiary that had a Replacement Status,
            // move that status onto their CURRENT payment history entry (the best
            // available signal for "which entry this was about"). Both sides of an
            // existing Replaced/Is Replacement pair are backfilled together via the
            // old ReplacedByBeneficiaryId/ReplacesBeneficiaryId pointers, which still
            // exist at this point in the migration. Beneficiaries with no current
            // entry (CurrentPaymentHistoryId IS NULL) have nothing to attach the
            // link to and are simply not carried forward.
            migrationBuilder.Sql(@"
                UPDATE ph
                SET ph.ReplacementStatus = b.ReplacementStatus,
                    ph.ReplacementDate = b.ReplacementDate,
                    ph.ReplacementRemarks = b.ReplacementRemarks,
                    ph.ReplacedByPaymentHistoryId = CASE WHEN b.ReplacementStatus = 1 THEN rb.CurrentPaymentHistoryId ELSE ph.ReplacedByPaymentHistoryId END,
                    ph.ReplacesPaymentHistoryId = CASE WHEN b.ReplacementStatus = 2 THEN rs.CurrentPaymentHistoryId ELSE ph.ReplacesPaymentHistoryId END
                FROM BeneficiaryPaymentHistories ph
                INNER JOIN BeneficiaryInformations b ON b.CurrentPaymentHistoryId = ph.Id
                LEFT JOIN BeneficiaryInformations rb ON rb.Id = b.ReplacedByBeneficiaryId
                LEFT JOIN BeneficiaryInformations rs ON rs.Id = b.ReplacesBeneficiaryId
                WHERE b.ReplacementStatus IN (1, 2) AND b.CurrentPaymentHistoryId IS NOT NULL;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiary_ReplacementStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacedByBeneficiaryId",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacementDate",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacementRemarks",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacementStatus",
                table: "BeneficiaryInformations");

            migrationBuilder.DropColumn(
                name: "ReplacesBeneficiaryId",
                table: "BeneficiaryInformations");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistory_Beneficiary_ReplacementStatus",
                table: "BeneficiaryPaymentHistories",
                columns: new[] { "BeneficiaryInformationId", "ReplacementStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentHistory_Beneficiary_ReplacementStatus",
                table: "BeneficiaryPaymentHistories");

            migrationBuilder.DropColumn(
                name: "ReplacedByPaymentHistoryId",
                table: "BeneficiaryPaymentHistories");

            migrationBuilder.DropColumn(
                name: "ReplacementDate",
                table: "BeneficiaryPaymentHistories");

            migrationBuilder.DropColumn(
                name: "ReplacementRemarks",
                table: "BeneficiaryPaymentHistories");

            migrationBuilder.DropColumn(
                name: "ReplacementStatus",
                table: "BeneficiaryPaymentHistories");

            migrationBuilder.DropColumn(
                name: "ReplacesPaymentHistoryId",
                table: "BeneficiaryPaymentHistories");

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByBeneficiaryId",
                table: "BeneficiaryInformations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacementDate",
                table: "BeneficiaryInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementRemarks",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementStatus",
                table: "BeneficiaryInformations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesBeneficiaryId",
                table: "BeneficiaryInformations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiary_ReplacementStatus",
                table: "BeneficiaryInformations",
                columns: new[] { "IsDeleted", "ReplacementStatus" });
        }
    }
}
