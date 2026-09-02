using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeaderBodyBrandingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundImageUrl",
                table: "RestaurantSettings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyColor",
                table: "RestaurantSettings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#fafafa");

            migrationBuilder.AddColumn<string>(
                name: "CenterLogoUrl",
                table: "RestaurantSettings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderColor",
                table: "RestaurantSettings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#3f51b5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundImageUrl",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "BodyColor",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "CenterLogoUrl",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "HeaderColor",
                table: "RestaurantSettings");
        }
    }
}
