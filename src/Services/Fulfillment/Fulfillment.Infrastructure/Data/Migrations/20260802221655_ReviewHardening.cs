using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fulfillment.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                schema: "fulfillment",
                table: "Shipments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TrackingNumber",
                schema: "fulfillment",
                table: "Shipments",
                column: "TrackingNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_TrackingNumber",
                schema: "fulfillment",
                table: "Shipments");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                schema: "fulfillment",
                table: "Shipments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
