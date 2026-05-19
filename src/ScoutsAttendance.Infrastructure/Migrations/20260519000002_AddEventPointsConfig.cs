using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPointsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename existing columns to clearer names
            migrationBuilder.RenameColumn(
                name: "PointValue",
                table: "Events",
                newName: "PresentPoints");

            migrationBuilder.RenameColumn(
                name: "LatePointValue",
                table: "Events",
                newName: "LatePoints");

            // Add new per-status point columns
            migrationBuilder.AddColumn<decimal>(
                name: "ExcusedPoints",
                table: "Events",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "AbsentPoints",
                table: "Events",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: -10m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcusedPoints",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AbsentPoints",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "PresentPoints",
                table: "Events",
                newName: "PointValue");

            migrationBuilder.RenameColumn(
                name: "LatePoints",
                table: "Events",
                newName: "LatePointValue");
        }
    }
}
