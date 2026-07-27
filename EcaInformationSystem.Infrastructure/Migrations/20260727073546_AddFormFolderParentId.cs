using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFormFolderParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentFolderId",
                table: "FormFolders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormFolders_ParentFolderId",
                table: "FormFolders",
                column: "ParentFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormFolders_FormFolders_ParentFolderId",
                table: "FormFolders",
                column: "ParentFolderId",
                principalTable: "FormFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormFolders_FormFolders_ParentFolderId",
                table: "FormFolders");

            migrationBuilder.DropIndex(
                name: "IX_FormFolders_ParentFolderId",
                table: "FormFolders");

            migrationBuilder.DropColumn(
                name: "ParentFolderId",
                table: "FormFolders");
        }
    }
}
