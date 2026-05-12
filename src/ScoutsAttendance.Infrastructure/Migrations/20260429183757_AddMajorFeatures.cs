using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMajorFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherPhone",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasNeckerchief",
                table: "Members",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MotherPhone",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TalaeaId",
                table: "Members",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearJoined",
                table: "Members",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PointValue",
                table: "Events",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.CreateTable(
                name: "MemberExcuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberExcuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberExcuses_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Talaeas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TroopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Talaeas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Talaeas_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Talaeas_Troops_TroopId",
                        column: x => x.TroopId,
                        principalTable: "Troops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalaeaPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TalaeaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalaeaPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalaeaPoints_PointCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PointCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalaeaPoints_Talaeas_TalaeaId",
                        column: x => x.TalaeaId,
                        principalTable: "Talaeas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_TalaeaId",
                table: "Members",
                column: "TalaeaId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberExcuses_MemberId",
                table: "MemberExcuses",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberExcuses_MemberId_IsActive",
                table: "MemberExcuses",
                columns: new[] { "MemberId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TalaeaPoints_CategoryId",
                table: "TalaeaPoints",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TalaeaPoints_TalaeaId",
                table: "TalaeaPoints",
                column: "TalaeaId");

            migrationBuilder.CreateIndex(
                name: "IX_Talaeas_GroupId",
                table: "Talaeas",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Talaeas_TroopId",
                table: "Talaeas",
                column: "TroopId");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Talaeas_TalaeaId",
                table: "Members",
                column: "TalaeaId",
                principalTable: "Talaeas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_Talaeas_TalaeaId",
                table: "Members");

            migrationBuilder.DropTable(
                name: "MemberExcuses");

            migrationBuilder.DropTable(
                name: "TalaeaPoints");

            migrationBuilder.DropTable(
                name: "Talaeas");

            migrationBuilder.DropIndex(
                name: "IX_Members_TalaeaId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "FatherPhone",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "HasNeckerchief",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MotherPhone",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "TalaeaId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "YearJoined",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PointValue",
                table: "Events");
        }
    }
}
