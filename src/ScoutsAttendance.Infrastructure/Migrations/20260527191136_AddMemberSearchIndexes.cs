using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Members",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Members",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Members_GroupId_FirstName_LastName",
                table: "Members",
                columns: new[] { "GroupId", "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Members_GroupId_LastName_FirstName",
                table: "Members",
                columns: new[] { "GroupId", "LastName", "FirstName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_GroupId_FirstName_LastName",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Members_GroupId_LastName_FirstName",
                table: "Members");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Members",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Members",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
