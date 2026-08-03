using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Data.Migrations;

public partial class OrderStatusLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Previous enum values were Pending=1, Paid=2, Cancelled=3, Completed=4.
        // Cancelled is now 6; value 4 intentionally remains Shipped.
        migrationBuilder.Sql(
            "UPDATE order.\"Orders\" SET \"Status\" = 6 WHERE \"Status\" = 3;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE order.\"Orders\" SET \"Status\" = 3 WHERE \"Status\" = 6;");
    }
}
