using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFocalInviteAndFocalRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FocalInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocalInvites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FocalInviteJurisdictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FocalInviteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PdoJurisdictionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocalInviteJurisdictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FocalInviteJurisdictions_FocalInvites_FocalInviteId",
                        column: x => x.FocalInviteId,
                        principalTable: "FocalInvites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FocalInviteJurisdictions_PdoJurisdictions_PdoJurisdictionId",
                        column: x => x.PdoJurisdictionId,
                        principalTable: "PdoJurisdictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocalInviteJurisdictions_FocalInviteId",
                table: "FocalInviteJurisdictions",
                column: "FocalInviteId");

            migrationBuilder.CreateIndex(
                name: "IX_FocalInviteJurisdictions_PdoJurisdictionId",
                table: "FocalInviteJurisdictions",
                column: "PdoJurisdictionId");

            migrationBuilder.CreateIndex(
                name: "IX_FocalInvites_InvitedByUserId",
                table: "FocalInvites",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FocalInvites_Status",
                table: "FocalInvites",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocalInviteJurisdictions");

            migrationBuilder.DropTable(
                name: "FocalInvites");
        }
    }
}
