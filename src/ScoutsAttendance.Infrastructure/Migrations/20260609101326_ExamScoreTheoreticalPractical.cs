using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExamScoreTheoreticalPractical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new columns first (default 0)
            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalScore",
                table: "MemberExamScores",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PracticalScore",
                table: "MemberExamScores",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            // 2. Data migration: move existing Score into TheoreticalScore; PracticalScore stays 0
            // Works on both SQL Server (bit: 0/1) and PostgreSQL (boolean: false/true)
            migrationBuilder.Sql(
                "UPDATE \"MemberExamScores\" SET \"TheoreticalScore\" = \"Score\"");

            // 3. Drop the old Score column
            migrationBuilder.DropColumn(
                name: "Score",
                table: "MemberExamScores");

            migrationBuilder.CreateTable(
                name: "ExamScoreConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TheoreticalMaxScore = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 50m),
                    PracticalMaxScore = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 50m),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamScoreConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamScoreConfigs_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamScoreConfigs_GroupId_Year",
                table: "ExamScoreConfigs",
                columns: new[] { "GroupId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamScoreConfigs");

            migrationBuilder.DropColumn(
                name: "PracticalScore",
                table: "MemberExamScores");

            migrationBuilder.DropColumn(
                name: "TheoreticalScore",
                table: "MemberExamScores");

            migrationBuilder.AddColumn<decimal>(
                name: "Score",
                table: "MemberExamScores",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
