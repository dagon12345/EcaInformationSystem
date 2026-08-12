using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceCallLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoiceCallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CalleeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalleeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NotesUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceCallLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceCallLogs_CalleeId",
                table: "VoiceCallLogs",
                column: "CalleeId");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceCallLogs_CallerId",
                table: "VoiceCallLogs",
                column: "CallerId");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceCallLogs_StartedAt",
                table: "VoiceCallLogs",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoiceCallLogs");
        }
    }
}
