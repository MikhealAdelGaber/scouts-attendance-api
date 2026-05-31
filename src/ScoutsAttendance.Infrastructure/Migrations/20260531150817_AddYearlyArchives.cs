using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddYearlyArchives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YearlyArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalMembers = table.Column<int>(type: "int", nullable: false),
                    TotalGroups = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyMemberArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    YearlyArchiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TroopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TroopName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalPointsAtYearEnd = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalAttendanceCount = table.Column<int>(type: "int", nullable: false),
                    TotalEventsAttended = table.Column<int>(type: "int", nullable: false),
                    TotalExcusesCount = table.Column<int>(type: "int", nullable: false),
                    AcademicGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyMemberArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyMemberArchives_YearlyArchives_YearlyArchiveId",
                        column: x => x.YearlyArchiveId,
                        principalTable: "YearlyArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchives_ArchiveYear",
                table: "YearlyArchives",
                column: "ArchiveYear");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyMemberArchives_GroupId",
                table: "YearlyMemberArchives",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyMemberArchives_MemberId",
                table: "YearlyMemberArchives",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyMemberArchives_YearlyArchiveId",
                table: "YearlyMemberArchives",
                column: "YearlyArchiveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YearlyMemberArchives");

            migrationBuilder.DropTable(
                name: "YearlyArchives");
        }
    }
}
