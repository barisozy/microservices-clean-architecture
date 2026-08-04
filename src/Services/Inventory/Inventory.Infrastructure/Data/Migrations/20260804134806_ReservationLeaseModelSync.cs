using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReservationLeaseModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommittedAt",
                schema: "inventory",
                table: "InventoryReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiredAt",
                schema: "inventory",
                table: "InventoryReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "inventory",
                table: "InventoryReservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() + interval '100 years'");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                schema: "inventory",
                table: "InventoryReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "inventory",
                table: "InventoryReservations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE inventory.\"InventoryReservations\" SET \"Status\" = CASE WHEN \"IsReleased\" THEN 2 ELSE 1 END;");

            migrationBuilder.DropColumn(
                name: "IsReleased",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_Pending_ExpiresAt",
                schema: "inventory",
                table: "InventoryReservations",
                column: "ExpiresAt",
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_Pending_ExpiresAt",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "CommittedAt",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.AddColumn<bool>(
                name: "IsReleased",
                schema: "inventory",
                table: "InventoryReservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
