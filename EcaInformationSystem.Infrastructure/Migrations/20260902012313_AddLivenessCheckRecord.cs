using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLivenessCheckRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LivenessCheckRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhotoData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PhotoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LivenessCheckRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LivenessCheckRecords_BeneficiaryInformations_BeneficiaryInformationId",
                        column: x => x.BeneficiaryInformationId,
                        principalTable: "BeneficiaryInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LivenessCheckRecords_BeneficiaryInformationId_IsActive",
                table: "LivenessCheckRecords",
                columns: new[] { "BeneficiaryInformationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LivenessCheckRecords_Token",
                table: "LivenessCheckRecords",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LivenessCheckRecords");
        }
    }
}
