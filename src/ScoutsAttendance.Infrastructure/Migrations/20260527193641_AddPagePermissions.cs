using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPagePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanAccessAttendance",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessEvents",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessExamScores",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessExcuses",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessLeaderboard",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessMembers",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessPoints",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessReports",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessTroops",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanAccessAttendance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessEvents",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessExamScores",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessExcuses",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessLeaderboard",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessMembers",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessPoints",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessReports",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanAccessTroops",
                table: "Users");
        }
    }
}
