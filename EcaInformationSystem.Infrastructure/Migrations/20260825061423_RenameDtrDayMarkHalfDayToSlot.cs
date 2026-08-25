using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcaInformationSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameDtrDayMarkHalfDayToSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_DtrDayMark_User_Date",
                table: "DtrDayMarks");

            migrationBuilder.RenameColumn(
                name: "HalfDay",
                table: "DtrDayMarks",
                newName: "Slot");

            migrationBuilder.CreateIndex(
                name: "UQ_DtrDayMark_User_Date_Slot",
                table: "DtrDayMarks",
                columns: new[] { "UserId", "Date", "Slot" },
                unique: true,
                filter: "[Slot] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_DtrDayMark_User_Date_Slot",
                table: "DtrDayMarks");

            migrationBuilder.RenameColumn(
                name: "Slot",
                table: "DtrDayMarks",
                newName: "HalfDay");

            migrationBuilder.CreateIndex(
                name: "UQ_DtrDayMark_User_Date",
                table: "DtrDayMarks",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }
    }
}
