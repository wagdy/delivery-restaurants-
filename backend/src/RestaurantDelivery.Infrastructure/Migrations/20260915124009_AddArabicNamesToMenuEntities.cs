using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArabicNamesToMenuEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "SubCategories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "MenuItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "AddOns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "SubCategories");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "AddOns");
        }
    }
}
