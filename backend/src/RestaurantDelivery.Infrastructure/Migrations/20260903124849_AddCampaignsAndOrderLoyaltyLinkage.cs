using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignsAndOrderLoyaltyLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "LoyaltyPointTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TargetPunches = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyCampaignProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentPunches = table.Column<int>(type: "integer", nullable: false),
                    RewardsEarned = table.Column<int>(type: "integer", nullable: false),
                    LastPunchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyCampaignProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyCampaignProgress_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyCampaignProgress_LoyaltyCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "LoyaltyCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPunchTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CheckReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPunchTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyPunchTransactions_AspNetUsers_AdminId",
                        column: x => x.AdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoyaltyPunchTransactions_LoyaltyCampaignProgress_ProgressId",
                        column: x => x.ProgressId,
                        principalTable: "LoyaltyCampaignProgress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoyaltyPunchTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_OrderId",
                table: "LoyaltyPointTransactions",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCampaignProgress_CampaignId",
                table: "LoyaltyCampaignProgress",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCampaignProgress_CustomerId_CampaignId",
                table: "LoyaltyCampaignProgress",
                columns: new[] { "CustomerId", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCampaigns_IsActive",
                table: "LoyaltyCampaigns",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPunchTransactions_AdminId",
                table: "LoyaltyPunchTransactions",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPunchTransactions_CreatedAt",
                table: "LoyaltyPunchTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPunchTransactions_OrderId_ProgressId",
                table: "LoyaltyPunchTransactions",
                columns: new[] { "OrderId", "ProgressId" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPunchTransactions_ProgressId",
                table: "LoyaltyPunchTransactions",
                column: "ProgressId");

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyPointTransactions_Orders_OrderId",
                table: "LoyaltyPointTransactions",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyPointTransactions_Orders_OrderId",
                table: "LoyaltyPointTransactions");

            migrationBuilder.DropTable(
                name: "LoyaltyPunchTransactions");

            migrationBuilder.DropTable(
                name: "LoyaltyCampaignProgress");

            migrationBuilder.DropTable(
                name: "LoyaltyCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyPointTransactions_OrderId",
                table: "LoyaltyPointTransactions");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "LoyaltyPointTransactions");
        }
    }
}
