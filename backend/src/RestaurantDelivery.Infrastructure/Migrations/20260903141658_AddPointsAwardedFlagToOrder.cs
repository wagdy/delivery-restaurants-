using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPointsAwardedFlagToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PointsAwarded",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: orders that already earned loyalty points under the pre-flag logic
            // (tracked via LoyaltyPointTransaction.OrderId, unique since that feature
            // shipped) must not be eligible for re-processing if their status is ever
            // toggled away from and back to Delivered.
            migrationBuilder.Sql(
                """
                UPDATE "Orders"
                SET "PointsAwarded" = true
                WHERE "Id" IN (SELECT "OrderId" FROM "LoyaltyPointTransactions" WHERE "OrderId" IS NOT NULL)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsAwarded",
                table: "Orders");
        }
    }
}
