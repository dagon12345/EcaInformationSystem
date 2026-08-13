using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastSeasonNumber",
                table: "PendingUserRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSeasonRank",
                table: "PendingUserRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSeasonTransactionCount",
                table: "PendingUserRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeaderboardTotalWins",
                table: "PendingUserRegistrations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResetBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasons_SeasonNumber",
                table: "LeaderboardSeasons",
                column: "SeasonNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "LastSeasonNumber",
                table: "PendingUserRegistrations");

            migrationBuilder.DropColumn(
                name: "LastSeasonRank",
                table: "PendingUserRegistrations");

            migrationBuilder.DropColumn(
                name: "LastSeasonTransactionCount",
                table: "PendingUserRegistrations");

            migrationBuilder.DropColumn(
                name: "LeaderboardTotalWins",
                table: "PendingUserRegistrations");
        }
    }
}
