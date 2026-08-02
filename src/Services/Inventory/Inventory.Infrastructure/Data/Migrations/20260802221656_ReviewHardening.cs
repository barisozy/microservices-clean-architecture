using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "inventory",
                table: "Stocks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_Quantity_NonNegative",
                schema: "inventory",
                table: "Stocks",
                sql: "\"Quantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_ReservedQuantity_Range",
                schema: "inventory",
                table: "Stocks",
                sql: "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId_Sku",
                schema: "inventory",
                table: "InventoryReservations",
                columns: new[] { "OrderId", "Sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_Quantity_NonNegative",
                schema: "inventory",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_ReservedQuantity_Range",
                schema: "inventory",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_OrderId_Sku",
                schema: "inventory",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "inventory",
                table: "Stocks");
        }
    }
}
