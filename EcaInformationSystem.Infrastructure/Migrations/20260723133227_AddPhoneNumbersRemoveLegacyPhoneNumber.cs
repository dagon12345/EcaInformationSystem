using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumbersRemoveLegacyPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ Table is created BEFORE the old column is dropped, and sized generously
            // (50, not 11) up front — legacy free-text values aren't all clean 11-digit
            // numbers. Some rows hold multiple numbers glued together with no
            // recognizable delimiter at all (e.g. "09365696975097624469" — 21 chars,
            // no comma/slash to split on), so this needs real headroom, not just
            // enough for "11 digits + a couple of extras".
            migrationBuilder.CreateTable(
                name: "BeneficiaryPhoneNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryPhoneNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryPhoneNumbers_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneNumber_BeneficiaryInformationId",
                table: "BeneficiaryPhoneNumbers",
                column: "BeneficiaryInformationId");

            // ✅ Data migration — copies every existing PhoneNumber value into the new
            // table before the column is dropped below. Handles the free-text formats
            // found in production data: multiple numbers separated by "," or "/",
            // stray dashes/spaces, and 10-digit numbers missing the leading "0"
            // (e.g. "9171234567" -> "09171234567"). Values that still don't end up as
            // a clean 11-digit PH number after this are kept as-is rather than
            // discarded — this is a best-effort recovery of free-text history, not a
            // re-validation; new/edited entries are validated at the application layer.
            migrationBuilder.Sql(@"
;WITH norm AS (
    SELECT b.Id AS BeneficiaryInformationId,
           LTRIM(RTRIM(value)) AS RawToken
    FROM BeneficiaryInformations b
    CROSS APPLY STRING_SPLIT(REPLACE(REPLACE(REPLACE(b.PhoneNumber,'/',','),'-',''),' ',''), ',')
    WHERE b.PhoneNumber IS NOT NULL AND LTRIM(RTRIM(b.PhoneNumber)) <> ''
),
final AS (
    SELECT BeneficiaryInformationId,
           CASE WHEN LEN(RawToken) = 10 AND LEFT(RawToken, 1) = '9' THEN '0' + RawToken ELSE RawToken END AS Number
    FROM norm
    WHERE RawToken <> ''
),
ranked AS (
    SELECT BeneficiaryInformationId, Number,
           ROW_NUMBER() OVER (PARTITION BY BeneficiaryInformationId ORDER BY Number) - 1 AS SortOrder
    FROM final
)
INSERT INTO BeneficiaryPhoneNumbers (Id, BeneficiaryInformationId, Number, SortOrder)
SELECT NEWID(), BeneficiaryInformationId, LEFT(Number, 100), SortOrder
FROM ranked;
");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "BeneficiaryInformations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "BeneficiaryInformations",
                type: "nvarchar(max)",
                nullable: true);

            // ✅ Best-effort reverse of the Up() data migration — joins each
            // beneficiary's numbers back into a single comma-separated string.
            // Ordering/sort info is lost on the round trip, but no values are.
            migrationBuilder.Sql(@"
UPDATE b
SET b.PhoneNumber = joined.Numbers
FROM BeneficiaryInformations b
CROSS APPLY (
    SELECT STRING_AGG(p.Number, ', ') WITHIN GROUP (ORDER BY p.SortOrder) AS Numbers
    FROM BeneficiaryPhoneNumbers p
    WHERE p.BeneficiaryInformationId = b.Id
) joined
WHERE joined.Numbers IS NOT NULL;
");

            migrationBuilder.DropTable(
                name: "BeneficiaryPhoneNumbers");
        }
    }
}
