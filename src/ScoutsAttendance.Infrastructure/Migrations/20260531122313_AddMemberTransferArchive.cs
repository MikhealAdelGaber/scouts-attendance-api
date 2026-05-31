using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberTransferArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Groups_FromGroupId",
                table: "TransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Groups_ToGroupId",
                table: "TransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Members_MemberId",
                table: "TransferRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TransferRequests",
                table: "TransferRequests");

            migrationBuilder.RenameTable(
                name: "TransferRequests",
                newName: "MemberTransferRequests");

            migrationBuilder.RenameIndex(
                name: "IX_TransferRequests_ToGroupId",
                table: "MemberTransferRequests",
                newName: "IX_MemberTransferRequests_ToGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_TransferRequests_Status",
                table: "MemberTransferRequests",
                newName: "IX_MemberTransferRequests_Status");

            migrationBuilder.RenameIndex(
                name: "IX_TransferRequests_MemberId",
                table: "MemberTransferRequests",
                newName: "IX_MemberTransferRequests_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_TransferRequests_FromGroupId",
                table: "MemberTransferRequests",
                newName: "IX_MemberTransferRequests_FromGroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MemberTransferRequests",
                table: "MemberTransferRequests",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "MemberTransferArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromGroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToGroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalPointsAtTransfer = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalAttendanceCount = table.Column<int>(type: "int", nullable: false),
                    TotalEventsAttended = table.Column<int>(type: "int", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberTransferArchives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberTransferArchives_FromGroupId",
                table: "MemberTransferArchives",
                column: "FromGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberTransferArchives_MemberId",
                table: "MemberTransferArchives",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberTransferArchives_ToGroupId",
                table: "MemberTransferArchives",
                column: "ToGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberTransferRequests_Groups_FromGroupId",
                table: "MemberTransferRequests",
                column: "FromGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberTransferRequests_Groups_ToGroupId",
                table: "MemberTransferRequests",
                column: "ToGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberTransferRequests_Members_MemberId",
                table: "MemberTransferRequests",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberTransferRequests_Groups_FromGroupId",
                table: "MemberTransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MemberTransferRequests_Groups_ToGroupId",
                table: "MemberTransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MemberTransferRequests_Members_MemberId",
                table: "MemberTransferRequests");

            migrationBuilder.DropTable(
                name: "MemberTransferArchives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MemberTransferRequests",
                table: "MemberTransferRequests");

            migrationBuilder.RenameTable(
                name: "MemberTransferRequests",
                newName: "TransferRequests");

            migrationBuilder.RenameIndex(
                name: "IX_MemberTransferRequests_ToGroupId",
                table: "TransferRequests",
                newName: "IX_TransferRequests_ToGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_MemberTransferRequests_Status",
                table: "TransferRequests",
                newName: "IX_TransferRequests_Status");

            migrationBuilder.RenameIndex(
                name: "IX_MemberTransferRequests_MemberId",
                table: "TransferRequests",
                newName: "IX_TransferRequests_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_MemberTransferRequests_FromGroupId",
                table: "TransferRequests",
                newName: "IX_TransferRequests_FromGroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TransferRequests",
                table: "TransferRequests",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Groups_FromGroupId",
                table: "TransferRequests",
                column: "FromGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Groups_ToGroupId",
                table: "TransferRequests",
                column: "ToGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Members_MemberId",
                table: "TransferRequests",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
