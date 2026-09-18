using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteAndOrderItemNameSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MenuItemName",
                table: "OrderItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MenuItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill, and it is not optional. The column lands defaulted to "" on every
            // row that already exists, so without this every historical receipt would
            // show a blank item name the moment this deploys - which is the exact damage
            // the snapshot was added to prevent. Reads the name straight from the menu
            // item each row already points at.
            migrationBuilder.Sql(@"
                UPDATE ""OrderItems"" oi
                SET ""MenuItemName"" = m.""Name""
                FROM ""MenuItems"" m
                WHERE oi.""MenuItemId"" = m.""Id""
                  AND (oi.""MenuItemName"" IS NULL OR oi.""MenuItemName"" = '');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MenuItemName",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Categories");
        }
    }
}
