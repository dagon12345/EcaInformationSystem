using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionCodeToBiometricTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegionCode",
                table: "BiometricSyncStatuses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RegionCode",
                table: "BiometricDeviceUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RegionCode",
                table: "BiometricDeviceSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BiometricSyncStatus_RegionCode",
                table: "BiometricSyncStatuses",
                column: "RegionCode");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricDeviceUser_RegionCode",
                table: "BiometricDeviceUsers",
                column: "RegionCode");

            migrationBuilder.CreateIndex(
                name: "UQ_BiometricDeviceSetting_RegionCode",
                table: "BiometricDeviceSettings",
                column: "RegionCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BiometricSyncStatus_RegionCode",
                table: "BiometricSyncStatuses");

            migrationBuilder.DropIndex(
                name: "IX_BiometricDeviceUser_RegionCode",
                table: "BiometricDeviceUsers");

            migrationBuilder.DropIndex(
                name: "UQ_BiometricDeviceSetting_RegionCode",
                table: "BiometricDeviceSettings");

            migrationBuilder.DropColumn(
                name: "RegionCode",
                table: "BiometricSyncStatuses");

            migrationBuilder.DropColumn(
                name: "RegionCode",
                table: "BiometricDeviceUsers");

            migrationBuilder.DropColumn(
                name: "RegionCode",
                table: "BiometricDeviceSettings");
        }
    }
}
