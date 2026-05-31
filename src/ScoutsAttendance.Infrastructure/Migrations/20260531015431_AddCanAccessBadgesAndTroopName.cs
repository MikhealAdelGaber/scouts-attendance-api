using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanAccessBadgesAndTroopName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanAccessBadges",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TroopName",
                table: "MemberBadges",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanAccessBadges",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TroopName",
                table: "MemberBadges");
        }
    }
}
