using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnnualGranteeTargetQuarterly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AprTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "AugTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "DecTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "FebTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "JanTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "JulTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "JunTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.DropColumn(
                name: "MarTarget",
                table: "AnnualGranteeTargets");

            migrationBuilder.RenameColumn(
                name: "SepTarget",
                table: "AnnualGranteeTargets",
                newName: "Q4Target");

            migrationBuilder.RenameColumn(
                name: "OctTarget",
                table: "AnnualGranteeTargets",
                newName: "Q3Target");

            migrationBuilder.RenameColumn(
                name: "NovTarget",
                table: "AnnualGranteeTargets",
                newName: "Q2Target");

            migrationBuilder.RenameColumn(
                name: "MayTarget",
                table: "AnnualGranteeTargets",
                newName: "Q1Target");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Q4Target",
                table: "AnnualGranteeTargets",
                newName: "SepTarget");

            migrationBuilder.RenameColumn(
                name: "Q3Target",
                table: "AnnualGranteeTargets",
                newName: "OctTarget");

            migrationBuilder.RenameColumn(
                name: "Q2Target",
                table: "AnnualGranteeTargets",
                newName: "NovTarget");

            migrationBuilder.RenameColumn(
                name: "Q1Target",
                table: "AnnualGranteeTargets",
                newName: "MayTarget");

            migrationBuilder.AddColumn<int>(
                name: "AprTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AugTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DecTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FebTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JanTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JulTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JunTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MarTarget",
                table: "AnnualGranteeTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
