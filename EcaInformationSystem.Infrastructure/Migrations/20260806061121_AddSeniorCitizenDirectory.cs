using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorCitizenDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeniorCitizenDirectoryEntryId",
                table: "Logs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeniorCitizenDirectoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PsgcCodeRegion = table.Column<int>(type: "int", nullable: false),
                    PsgcCodeProvince = table.Column<int>(type: "int", nullable: false),
                    PsgcCodeMunicipality = table.Column<int>(type: "int", nullable: false),
                    IncomeClassification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeniorCitizensPopulation = table.Column<int>(type: "int", nullable: true),
                    PopulationAsOfNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LswdoName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LswdoPosition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LswdoContactNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LswdoEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScFocalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScFocalContactNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScFocalEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OscaHeadName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OscaHeadLengthOfService = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OscaHeadContactNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OscaHeadEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FscapPresidentName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FscapPresidentContactNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FscapPresidentEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FscapPresidentLengthOfService = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasSeniorCitizenCenter = table.Column<bool>(type: "bit", nullable: true),
                    IsSccAccredited = table.Column<bool>(type: "bit", nullable: true),
                    SccAccreditationValidity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WithoutSccResourcesNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SccManagedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServicesOffered = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasCashIncentive = table.Column<bool>(type: "bit", nullable: true),
                    CashIncentiveDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasSupportingOrdinance = table.Column<bool>(type: "bit", nullable: true),
                    OrdinanceDocumentLinks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasVaopHelpDesk = table.Column<bool>(type: "bit", nullable: true),
                    VaopReferralMechanism = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MayorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MayorOfficeEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeniorCitizenDirectoryEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Log_SeniorCitizenDirectoryEntryId",
                table: "Logs",
                column: "SeniorCitizenDirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SeniorCitizenDirectory_Municipality",
                table: "SeniorCitizenDirectoryEntries",
                columns: new[] { "IsDeleted", "PsgcCodeMunicipality" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeniorCitizenDirectoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_Log_SeniorCitizenDirectoryEntryId",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "SeniorCitizenDirectoryEntryId",
                table: "Logs");
        }
    }
}
