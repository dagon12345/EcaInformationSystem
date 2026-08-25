using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdayGreetingPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthdayTurningAge",
                table: "Posts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BirthdayUserId",
                table: "Posts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthdayUserName",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastBirthdayGreetedYear",
                table: "PendingUserRegistrations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthdayTurningAge",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "BirthdayUserId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "BirthdayUserName",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "LastBirthdayGreetedYear",
                table: "PendingUserRegistrations");
        }
    }
}
