using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SettingUSernametoUniqueAndIndexed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingUserRegistrations_UserName",
                table: "PendingUserRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingUserRegistrations_UserName",
                table: "PendingUserRegistrations",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingUserRegistrations_UserName",
                table: "PendingUserRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingUserRegistrations_UserName",
                table: "PendingUserRegistrations",
                column: "UserName");
        }
    }
}
