using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorePickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPickupEnabled",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PickupDuration",
                table: "RestaurantSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPickup",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPickupEnabled",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "PickupDuration",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "IsPickup",
                table: "Orders");
        }
    }
}
