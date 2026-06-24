using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MATGER.Api.Migrations
{
    /// <inheritdoc />
    public partial class StockAdjustmentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAdjustmentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityChange = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedInventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentRequests_InventoryMovements_AppliedInventoryMovementId",
                        column: x => x.AppliedInventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentRequests_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentRequests_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_AppliedInventoryMovementId",
                table: "StockAdjustmentRequests",
                column: "AppliedInventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_ProductId",
                table: "StockAdjustmentRequests",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_RequestedAtUtc",
                table: "StockAdjustmentRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_RequestedByUserId",
                table: "StockAdjustmentRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_ReviewedByUserId",
                table: "StockAdjustmentRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_Status",
                table: "StockAdjustmentRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentRequests_VariantId",
                table: "StockAdjustmentRequests",
                column: "VariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAdjustmentRequests");
        }
    }
}
