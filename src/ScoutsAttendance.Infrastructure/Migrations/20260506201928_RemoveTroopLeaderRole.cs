using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTroopLeaderRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate legacy TroopLeader (3) → GroupLeader (2)
            migrationBuilder.Sql(
                "UPDATE [Users] SET [Role] = 2 WHERE [Role] = 3");

            // Migrate legacy Member (4) → AttendanceOnly (5)
            migrationBuilder.Sql(
                "UPDATE [Users] SET [Role] = 5 WHERE [Role] = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: GroupLeader (2) back to TroopLeader (3) is not safe
            // (all GroupLeaders are indistinguishable), so we leave this as a no-op
        }
    }
}
