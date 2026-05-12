using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTalaeaAddRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_Talaeas_TalaeaId",
                table: "Members");

            migrationBuilder.DropTable(
                name: "TalaeaPoints");

            migrationBuilder.DropTable(
                name: "Talaeas");

            migrationBuilder.DropIndex(
                name: "IX_Members_TalaeaId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "TalaeaId",
                table: "Members");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Region",
                table: "Members");

            migrationBuilder.AddColumn<Guid>(
                name: "TalaeaId",
                table: "Members",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Talaeas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TroopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TalaeaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Points = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
    }
}
