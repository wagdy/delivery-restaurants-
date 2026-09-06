using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyTiersAndConvertMembershipTierToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MembershipTier",
                table: "LoyaltyProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "LoyaltyTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinPoints = table.Column<int>(type: "integer", nullable: false),
                    MaxPoints = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTiers_MinPoints",
                table: "LoyaltyTiers",
                column: "MinPoints");

            // Seeds the exact 4 tiers the old hardcoded MembershipTier enum + calculator
            // used (see the now-deleted MembershipTierCalculator), matching the same
            // 500/1000/2500 thresholds - existing LoyaltyProfiles.MembershipTier string
            // values ("Bronze"/"Silver"/"Gold"/"VIP", already stored as plain strings
            // before this migration) keep resolving to a real, admin-editable tier
            // instead of becoming orphaned the moment this migration runs.
            migrationBuilder.InsertData(
                table: "LoyaltyTiers",
                columns: new[] { "Name", "MinPoints", "MaxPoints" },
                values: new object[,]
                {
                    { "Bronze", 0, 499 },
                    { "Silver", 500, 999 },
                    { "Gold", 1000, 2499 },
                    { "VIP", 2500, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyTiers");

            migrationBuilder.AlterColumn<string>(
                name: "MembershipTier",
                table: "LoyaltyProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
