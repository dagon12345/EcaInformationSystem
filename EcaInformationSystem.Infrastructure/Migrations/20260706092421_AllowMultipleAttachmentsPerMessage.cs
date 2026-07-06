using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleAttachmentsPerMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId",
                unique: true,
                filter: "[ChatMessageId] IS NOT NULL");
        }
    }
}
