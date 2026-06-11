using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedPdoJurisdictionAndModifyPendUserRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "PendingUserRegistrations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Viewer",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateTable(
                name: "PdoJurisdictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PsgcCodeMunicipality = table.Column<int>(type: "int", nullable: false),
                    MunicipalityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProvinceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdoJurisdictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdoJurisdictions_PendingUserRegistrations_UserId",
                        column: x => x.UserId,
                        principalTable: "PendingUserRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdoJurisdictions_PsgcCodeMunicipality",
                table: "PdoJurisdictions",
                column: "PsgcCodeMunicipality");

            migrationBuilder.CreateIndex(
                name: "IX_PdoJurisdictions_UserId",
                table: "PdoJurisdictions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PdoJurisdictions_UserId_PsgcCodeMunicipality",
                table: "PdoJurisdictions",
                columns: new[] { "UserId", "PsgcCodeMunicipality" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdoJurisdictions");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "PendingUserRegistrations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Viewer");
        }
    }
}
