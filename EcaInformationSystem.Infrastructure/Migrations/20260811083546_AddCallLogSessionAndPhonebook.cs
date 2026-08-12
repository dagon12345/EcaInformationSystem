using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCallLogSessionAndPhonebook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BeneficiaryInformationId",
                table: "CallLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "CallLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "CallLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PhonebookContactId",
                table: "CallLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "CallLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UsedBluetoothHeadset",
                table: "CallLogs",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhonebookContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhonebookContacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhonebookContact_UserId",
                table: "PhonebookContacts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhonebookContacts");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "CallLogs");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "CallLogs");

            migrationBuilder.DropColumn(
                name: "PhonebookContactId",
                table: "CallLogs");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "CallLogs");

            migrationBuilder.DropColumn(
                name: "UsedBluetoothHeadset",
                table: "CallLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "BeneficiaryInformationId",
                table: "CallLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
