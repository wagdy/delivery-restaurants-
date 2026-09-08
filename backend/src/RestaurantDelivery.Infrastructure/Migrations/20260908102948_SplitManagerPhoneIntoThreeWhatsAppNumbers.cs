using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitManagerPhoneIntoThreeWhatsAppNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ManagerPhoneNumber",
                table: "RestaurantSettings",
                newName: "ManagerWhatsApp3");

            migrationBuilder.AddColumn<string>(
                name: "ManagerWhatsApp1",
                table: "RestaurantSettings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerWhatsApp2",
                table: "RestaurantSettings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagerWhatsApp1",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "ManagerWhatsApp2",
                table: "RestaurantSettings");

            migrationBuilder.RenameColumn(
                name: "ManagerWhatsApp3",
                table: "RestaurantSettings",
                newName: "ManagerPhoneNumber");
        }
    }
}
