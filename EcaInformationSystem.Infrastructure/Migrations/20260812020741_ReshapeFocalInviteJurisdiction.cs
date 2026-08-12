using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReshapeFocalInviteJurisdiction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FocalInviteJurisdictions_PdoJurisdictions_PdoJurisdictionId",
                table: "FocalInviteJurisdictions");

            migrationBuilder.DropIndex(
                name: "IX_FocalInviteJurisdictions_PdoJurisdictionId",
                table: "FocalInviteJurisdictions");

            migrationBuilder.DropColumn(
                name: "PdoJurisdictionId",
                table: "FocalInviteJurisdictions");

            migrationBuilder.AddColumn<string>(
                name: "MunicipalityName",
                table: "FocalInviteJurisdictions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProvinceName",
                table: "FocalInviteJurisdictions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PsgcCodeMunicipality",
                table: "FocalInviteJurisdictions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MunicipalityName",
                table: "FocalInviteJurisdictions");

            migrationBuilder.DropColumn(
                name: "ProvinceName",
                table: "FocalInviteJurisdictions");

            migrationBuilder.DropColumn(
                name: "PsgcCodeMunicipality",
                table: "FocalInviteJurisdictions");

            migrationBuilder.AddColumn<Guid>(
                name: "PdoJurisdictionId",
                table: "FocalInviteJurisdictions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_FocalInviteJurisdictions_PdoJurisdictionId",
                table: "FocalInviteJurisdictions",
                column: "PdoJurisdictionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FocalInviteJurisdictions_PdoJurisdictions_PdoJurisdictionId",
                table: "FocalInviteJurisdictions",
                column: "PdoJurisdictionId",
                principalTable: "PdoJurisdictions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
