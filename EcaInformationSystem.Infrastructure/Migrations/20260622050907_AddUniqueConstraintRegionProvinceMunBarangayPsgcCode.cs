using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintRegionProvinceMunBarangayPsgcCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_Region_PsgcCode",
                table: "Regions",
                column: "PsgcCodeRegion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Province_PsgcCode",
                table: "Provinces",
                column: "PsgcCodeProvince",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Municipality_PsgcCode",
                table: "Municipalities",
                column: "PsgcCodeMunicipality",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Barangay_PsgcCode",
                table: "Barangays",
                column: "PsgcCodeBarangay",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Region_PsgcCode",
                table: "Regions");

            migrationBuilder.DropIndex(
                name: "UQ_Province_PsgcCode",
                table: "Provinces");

            migrationBuilder.DropIndex(
                name: "UQ_Municipality_PsgcCode",
                table: "Municipalities");

            migrationBuilder.DropIndex(
                name: "UQ_Barangay_PsgcCode",
                table: "Barangays");
        }
    }
}
