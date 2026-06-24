using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MATGER.Api.Migrations
{
    /// <inheritdoc />
    public partial class InventoryReorderPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BinLocation",
                table: "InventoryItems",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "InventoryItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReorderPoint",
                table: "InventoryItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReorderQuantity",
                table: "InventoryItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "InventoryItems",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierSku",
                table: "InventoryItems",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BinLocation",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ReorderPoint",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ReorderQuantity",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SupplierSku",
                table: "InventoryItems");
        }
    }
}
