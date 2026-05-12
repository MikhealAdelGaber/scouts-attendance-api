using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePointCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberPoints_PointCategories_CategoryId",
                table: "MemberPoints");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopPoints_PointCategories_CategoryId",
                table: "TroopPoints");

            migrationBuilder.DropIndex(
                name: "IX_TroopPoints_CategoryId",
                table: "TroopPoints");

            migrationBuilder.DropIndex(
                name: "IX_MemberPoints_CategoryId",
                table: "MemberPoints");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "TroopPoints");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "MemberPoints");

            migrationBuilder.AddColumn<Guid>(
                name: "TroopPointCategoryId",
                table: "TroopPoints",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberPointCategoryId",
                table: "MemberPoints",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MemberPointCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsGlobal = table.Column<bool>(type: "bit", nullable: false),
                    AttendancePresentPoints = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AttendanceLatePoints = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPointCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberPointCategories_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TroopPointCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsGlobal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopPointCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopPointCategories_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TroopPoints_TroopPointCategoryId",
                table: "TroopPoints",
                column: "TroopPointCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPoints_MemberPointCategoryId",
                table: "MemberPoints",
                column: "MemberPointCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPointCategories_GroupId",
                table: "MemberPointCategories",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopPointCategories_GroupId",
                table: "TroopPointCategories",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberPoints_MemberPointCategories_MemberPointCategoryId",
                table: "MemberPoints",
                column: "MemberPointCategoryId",
                principalTable: "MemberPointCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TroopPoints_TroopPointCategories_TroopPointCategoryId",
                table: "TroopPoints",
                column: "TroopPointCategoryId",
                principalTable: "TroopPointCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberPoints_MemberPointCategories_MemberPointCategoryId",
                table: "MemberPoints");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopPoints_TroopPointCategories_TroopPointCategoryId",
                table: "TroopPoints");

            migrationBuilder.DropTable(
                name: "MemberPointCategories");

            migrationBuilder.DropTable(
                name: "TroopPointCategories");

            migrationBuilder.DropIndex(
                name: "IX_TroopPoints_TroopPointCategoryId",
                table: "TroopPoints");

            migrationBuilder.DropIndex(
                name: "IX_MemberPoints_MemberPointCategoryId",
                table: "MemberPoints");

            migrationBuilder.DropColumn(
                name: "TroopPointCategoryId",
                table: "TroopPoints");

            migrationBuilder.DropColumn(
                name: "MemberPointCategoryId",
                table: "MemberPoints");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "TroopPoints",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "MemberPoints",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_TroopPoints_CategoryId",
                table: "TroopPoints",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPoints_CategoryId",
                table: "MemberPoints",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberPoints_PointCategories_CategoryId",
                table: "MemberPoints",
                column: "CategoryId",
                principalTable: "PointCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TroopPoints_PointCategories_CategoryId",
                table: "TroopPoints",
                column: "CategoryId",
                principalTable: "PointCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
