using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnualGranteeTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnualGranteeTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionCode = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    JanTarget = table.Column<int>(type: "int", nullable: false),
                    FebTarget = table.Column<int>(type: "int", nullable: false),
                    MarTarget = table.Column<int>(type: "int", nullable: false),
                    AprTarget = table.Column<int>(type: "int", nullable: false),
                    MayTarget = table.Column<int>(type: "int", nullable: false),
                    JunTarget = table.Column<int>(type: "int", nullable: false),
                    JulTarget = table.Column<int>(type: "int", nullable: false),
                    AugTarget = table.Column<int>(type: "int", nullable: false),
                    SepTarget = table.Column<int>(type: "int", nullable: false),
                    OctTarget = table.Column<int>(type: "int", nullable: false),
                    NovTarget = table.Column<int>(type: "int", nullable: false),
                    DecTarget = table.Column<int>(type: "int", nullable: false),
                    DateSet = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SetBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualGranteeTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_AnnualGranteeTarget_Region_FiscalYear",
                table: "AnnualGranteeTargets",
                columns: new[] { "RegionCode", "FiscalYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualGranteeTargets");
        }
    }
}
