using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFromFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "FormDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FormActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FormDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormDocuments_FolderId",
                table: "FormDocuments",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_FormActivityLogs_CreatedAt",
                table: "FormActivityLogs",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_FormActivityLogs_FolderId",
                table: "FormActivityLogs",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_FormActivityLogs_FormDocumentId",
                table: "FormActivityLogs",
                column: "FormDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FormFolders_IsDeleted",
                table: "FormFolders",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_FormFolders_Name",
                table: "FormFolders",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_FormDocuments_FormFolders_FolderId",
                table: "FormDocuments",
                column: "FolderId",
                principalTable: "FormFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormDocuments_FormFolders_FolderId",
                table: "FormDocuments");

            migrationBuilder.DropTable(
                name: "FormActivityLogs");

            migrationBuilder.DropTable(
                name: "FormFolders");

            migrationBuilder.DropIndex(
                name: "IX_FormDocuments_FolderId",
                table: "FormDocuments");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "FormDocuments");
        }
    }
}
