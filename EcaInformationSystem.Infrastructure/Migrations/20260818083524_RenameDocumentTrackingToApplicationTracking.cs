using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Hand-written rename (not the EF-scaffolded Drop+Create) so existing
    // ApplicationBatches/Rows/Transfers data survives the rename.
    public partial class RenameDocumentTrackingToApplicationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "DocumentBatches",
                newName: "ApplicationBatches");

            migrationBuilder.RenameTable(
                name: "DocumentGranteeRows",
                newName: "ApplicationGranteeRows");

            migrationBuilder.RenameTable(
                name: "DocumentTransfers",
                newName: "ApplicationTransfers");

            migrationBuilder.RenameColumn(
                name: "DocumentBatchId",
                table: "ApplicationGranteeRows",
                newName: "ApplicationBatchId");

            migrationBuilder.RenameColumn(
                name: "DocumentBatchId",
                table: "ApplicationTransfers",
                newName: "ApplicationBatchId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentBatches_PsgcCodeProvince_PsgcCodeMunicipality_MilestoneYear",
                table: "ApplicationBatches",
                newName: "IX_ApplicationBatches_PsgcCodeProvince_PsgcCodeMunicipality_MilestoneYear");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentGranteeRows_DocumentBatchId",
                table: "ApplicationGranteeRows",
                newName: "IX_ApplicationGranteeRows_ApplicationBatchId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentTransfers_DocumentBatchId",
                table: "ApplicationTransfers",
                newName: "IX_ApplicationTransfers_ApplicationBatchId");

            migrationBuilder.Sql("EXEC sp_rename N'PK_DocumentBatches', N'PK_ApplicationBatches', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_DocumentGranteeRows', N'PK_ApplicationGranteeRows', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_DocumentTransfers', N'PK_ApplicationTransfers', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_DocumentGranteeRows_DocumentBatches_DocumentBatchId', N'FK_ApplicationGranteeRows_ApplicationBatches_ApplicationBatchId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_DocumentTransfers_DocumentBatches_DocumentBatchId', N'FK_ApplicationTransfers_ApplicationBatches_ApplicationBatchId', N'OBJECT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC sp_rename N'FK_ApplicationTransfers_ApplicationBatches_ApplicationBatchId', N'FK_DocumentTransfers_DocumentBatches_DocumentBatchId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_ApplicationGranteeRows_ApplicationBatches_ApplicationBatchId', N'FK_DocumentGranteeRows_DocumentBatches_DocumentBatchId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_ApplicationTransfers', N'PK_DocumentTransfers', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_ApplicationGranteeRows', N'PK_DocumentGranteeRows', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_ApplicationBatches', N'PK_DocumentBatches', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_ApplicationTransfers_ApplicationBatchId",
                table: "ApplicationTransfers",
                newName: "IX_DocumentTransfers_DocumentBatchId");

            migrationBuilder.RenameIndex(
                name: "IX_ApplicationGranteeRows_ApplicationBatchId",
                table: "ApplicationGranteeRows",
                newName: "IX_DocumentGranteeRows_DocumentBatchId");

            migrationBuilder.RenameIndex(
                name: "IX_ApplicationBatches_PsgcCodeProvince_PsgcCodeMunicipality_MilestoneYear",
                table: "ApplicationBatches",
                newName: "IX_DocumentBatches_PsgcCodeProvince_PsgcCodeMunicipality_MilestoneYear");

            migrationBuilder.RenameColumn(
                name: "ApplicationBatchId",
                table: "ApplicationTransfers",
                newName: "DocumentBatchId");

            migrationBuilder.RenameColumn(
                name: "ApplicationBatchId",
                table: "ApplicationGranteeRows",
                newName: "DocumentBatchId");

            migrationBuilder.RenameTable(
                name: "ApplicationTransfers",
                newName: "DocumentTransfers");

            migrationBuilder.RenameTable(
                name: "ApplicationGranteeRows",
                newName: "DocumentGranteeRows");

            migrationBuilder.RenameTable(
                name: "ApplicationBatches",
                newName: "DocumentBatches");
        }
    }
}
