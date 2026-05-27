using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutsAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlexiblePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfInstallments",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "AmountDue",
                table: "BookingPayments");

            migrationBuilder.DropColumn(
                name: "InstallmentNumber",
                table: "BookingPayments");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidAt",
                table: "BookingPayments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfInstallments",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidAt",
                table: "BookingPayments",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountDue",
                table: "BookingPayments",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentNumber",
                table: "BookingPayments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
