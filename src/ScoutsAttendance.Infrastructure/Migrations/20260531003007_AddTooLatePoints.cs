using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTooLatePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TooLatePoints",
                table: "Events",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TooLatePoints",
                table: "Events");
        }
    }
}
