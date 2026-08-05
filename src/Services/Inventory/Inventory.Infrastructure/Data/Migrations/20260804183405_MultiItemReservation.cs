using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiItemReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_OrderId_Sku",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "inventory",
                table: "InventoryReservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InventoryReservationItems",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryReservationItems_InventoryReservations_InventoryRe~",
                        column: x => x.InventoryReservationId,
                        principalSchema: "inventory",
                        principalTable: "InventoryReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId",
                schema: "inventory",
                table: "InventoryReservations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_RequestFingerprint",
                schema: "inventory",
                table: "InventoryReservations",
                column: "RequestFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservationItems_InventoryReservationId_Sku",
                schema: "inventory",
                table: "InventoryReservationItems",
                columns: new[] { "InventoryReservationId", "Sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryReservationItems",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_OrderId",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_RequestFingerprint",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "inventory",
                table: "InventoryReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "inventory",
                table: "InventoryReservations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId_Sku",
                schema: "inventory",
                table: "InventoryReservations",
                columns: new[] { "OrderId", "Sku" },
                unique: true);
        }
    }
}
