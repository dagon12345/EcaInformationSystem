using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWfpEcaEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WfpEcaEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionCode = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    UacsCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Allotment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Obligation = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateSet = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SetBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfpEcaEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_WfpEcaEntry_Region_FiscalYear_UacsCode",
                table: "WfpEcaEntries",
                columns: new[] { "RegionCode", "FiscalYear", "UacsCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WfpEcaEntries");
        }
    }
}
