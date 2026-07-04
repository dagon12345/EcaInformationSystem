using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeChatMessageIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChatMessageId",
                table: "ChatAttachments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId",
                unique: true,
                filter: "[ChatMessageId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChatMessageId",
                table: "ChatAttachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId",
                unique: true);
        }
    }
}
