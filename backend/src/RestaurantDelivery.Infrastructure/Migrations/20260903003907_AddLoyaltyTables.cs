using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyPointTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    AdminId = table.Column<string>(type: "text", nullable: true),
                    PointsTransacted = table.Column<int>(type: "integer", nullable: false),
                    CheckAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPointTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyPointTransactions_AspNetUsers_AdminId",
                        column: x => x.AdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoyaltyPointTransactions_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyProfiles",
                columns: table => new
                {
                    AppUserId = table.Column<string>(type: "text", nullable: false),
                    CurrentPoints = table.Column<int>(type: "integer", nullable: false),
                    TotalLifetimePoints = table.Column<int>(type: "integer", nullable: false),
                    MembershipTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferralCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    HasGoogleWalletObject = table.Column<bool>(type: "boolean", nullable: false),
                    LastActivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyProfiles", x => x.AppUserId);
                    table.ForeignKey(
                        name: "FK_LoyaltyProfiles_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyWalletPassRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceLibraryIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PushToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PassTypeIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppUserId = table.Column<string>(type: "text", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPassUpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyWalletPassRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyWalletPassRegistrations_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_AdminId",
                table: "LoyaltyPointTransactions",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_CreatedAt",
                table: "LoyaltyPointTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_CustomerId",
                table: "LoyaltyPointTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyProfiles_ReferralCode",
                table: "LoyaltyProfiles",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyWalletPassRegistrations_AppUserId",
                table: "LoyaltyWalletPassRegistrations",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyWalletPassRegistrations_DeviceLibraryIdentifier_Pass~",
                table: "LoyaltyWalletPassRegistrations",
                columns: new[] { "DeviceLibraryIdentifier", "PassTypeIdentifier", "AppUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyPointTransactions");

            migrationBuilder.DropTable(
                name: "LoyaltyProfiles");

            migrationBuilder.DropTable(
                name: "LoyaltyWalletPassRegistrations");
        }
    }
}
