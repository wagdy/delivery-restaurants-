using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing rows all default to 0 above, which would tie every category at
            // the same order - backfill using current Id order (their original insertion
            // order) as a starting point so already-deployed databases still show a
            // sensible order until an admin explicitly reorders them.
            migrationBuilder.Sql(
                "UPDATE \"Categories\" SET \"DisplayOrder\" = ranked.\"RowNum\" - 1 " +
                "FROM (SELECT \"Id\", ROW_NUMBER() OVER (ORDER BY \"Id\") AS \"RowNum\" FROM \"Categories\") AS ranked " +
                "WHERE \"Categories\".\"Id\" = ranked.\"Id\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Categories");
        }
    }
}
