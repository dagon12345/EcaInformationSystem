using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageToReply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_ReplyToMessageId",
                table: "ChatMessages",
                column: "ReplyToMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessage_ReplyToMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "ChatMessages");
        }
    }
}
