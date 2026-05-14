using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeMemberTroopIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_Troops_TroopId",
                table: "Members");

            migrationBuilder.AlterColumn<Guid>(
                name: "TroopId",
                table: "Members",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Troops_TroopId",
                table: "Members",
                column: "TroopId",
                principalTable: "Troops",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_Troops_TroopId",
                table: "Members");

            migrationBuilder.AlterColumn<Guid>(
                name: "TroopId",
                table: "Members",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Troops_TroopId",
                table: "Members",
                column: "TroopId",
                principalTable: "Troops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
