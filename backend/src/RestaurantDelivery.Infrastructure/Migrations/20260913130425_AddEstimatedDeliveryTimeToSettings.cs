using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedDeliveryTimeToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedDeliveryMaxMinutes",
                table: "RestaurantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 45);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDeliveryMinMinutes",
                table: "RestaurantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedDeliveryMaxMinutes",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "EstimatedDeliveryMinMinutes",
                table: "RestaurantSettings");
        }
    }
}
