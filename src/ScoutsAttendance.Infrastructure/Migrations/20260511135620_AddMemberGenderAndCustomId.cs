using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberGenderAndCustomId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Gender (default 1 = Male)
            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "Members",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Add CustomId without unique constraint first (existing rows will be 0)
            migrationBuilder.AddColumn<int>(
                name: "CustomId",
                table: "Members",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill existing members with sequential odd CustomIds (treated as Male)
            // Uses ROW_NUMBER to assign 100001, 100003, 100005 … to all existing rows
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // PostgreSQL syntax
                migrationBuilder.Sql(@"
                    WITH numbered AS (
                        SELECT ""Id"", ROW_NUMBER() OVER (ORDER BY ""CreatedAt"", ""Id"") AS rn
                        FROM ""Members""
                        WHERE ""CustomId"" = 0
                    )
                    UPDATE ""Members""
                    SET ""CustomId"" = 100001 + ((numbered.rn - 1) * 2)
                    FROM numbered
                    WHERE ""Members"".""Id"" = numbered.""Id"";
                ");
            }
            else
            {
                // SQL Server syntax
                migrationBuilder.Sql(@"
                    WITH numbered AS (
                        SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS rn
                        FROM Members
                        WHERE CustomId = 0
                    )
                    UPDATE Members
                    SET CustomId = 100001 + ((numbered.rn - 1) * 2)
                    FROM Members
                    INNER JOIN numbered ON Members.Id = numbered.Id;
                ");
            }

            // Now it is safe to create the unique index
            migrationBuilder.CreateIndex(
                name: "IX_Members_CustomId",
                table: "Members",
                column: "CustomId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_CustomId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "CustomId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Members");
        }
    }
}
