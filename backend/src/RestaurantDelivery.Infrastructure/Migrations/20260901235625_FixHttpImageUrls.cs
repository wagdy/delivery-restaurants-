using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixHttpImageUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only fix, no schema change: SettingsController/MenuItemsController
            // built these absolute URLs from Request.Scheme before Program.cs trusted
            // Railway's X-Forwarded-Proto header, so every image uploaded before that
            // fix got stored as "http://..." even though it's actually served over
            // https - triggering a mixed-content warning on every page load. Only rows
            // that literally start with "http://" are touched; already-correct https
            // URLs and relative paths are left alone, so this is safe to run more than
            // once.
            migrationBuilder.Sql(
                "UPDATE \"RestaurantSettings\" SET \"LogoUrl\" = 'https://' || SUBSTRING(\"LogoUrl\" FROM 8) " +
                "WHERE \"LogoUrl\" LIKE 'http://%';");

            migrationBuilder.Sql(
                "UPDATE \"MenuItems\" SET \"ImageUrl\" = 'https://' || SUBSTRING(\"ImageUrl\" FROM 8) " +
                "WHERE \"ImageUrl\" LIKE 'http://%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately not reversed - reverting to known-wrong http:// URLs on a
            // rollback would just reintroduce the bug this migration fixes.
        }
    }
}
