using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedChatFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Region",
                table: "PendingUserRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatReadStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatReadStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RegionCode = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsEveryoneMention = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ThumbnailData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatAttachments_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsEveryoneMention = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMentions_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMention_ChatMessageId",
                table: "ChatMentions",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMention_MentionedUserId",
                table: "ChatMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_Room_SentAt",
                table: "ChatMessages",
                columns: new[] { "RoomId", "SentAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_SenderId",
                table: "ChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "UQ_ChatReadStatus_RoomUser",
                table: "ChatReadStatuses",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMember_UserId",
                table: "ChatRoomMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_ChatRoomMember_RoomUser",
                table: "ChatRoomMembers",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ChatRoom_OneRoomPerRegion",
                table: "ChatRooms",
                columns: new[] { "Type", "RegionCode" },
                unique: true,
                filter: "[Type] = 1");

            migrationBuilder.CreateIndex(
                name: "UQ_ChatRoom_SingleGlobalRoom",
                table: "ChatRooms",
                column: "Type",
                unique: true,
                filter: "[Type] = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatAttachments");

            migrationBuilder.DropTable(
                name: "ChatMentions");

            migrationBuilder.DropTable(
                name: "ChatReadStatuses");

            migrationBuilder.DropTable(
                name: "ChatRoomMembers");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "PendingUserRegistrations");
        }
    }
}
