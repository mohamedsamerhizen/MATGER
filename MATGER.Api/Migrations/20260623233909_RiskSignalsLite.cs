using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MATGER.Api.Migrations
{
    /// <inheritdoc />
    public partial class RiskSignalsLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskSignals_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskSignals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskSignals_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_CreatedAtUtc",
                table: "RiskSignals",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_OrderId",
                table: "RiskSignals",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_ReviewedByUserId",
                table: "RiskSignals",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_Severity",
                table: "RiskSignals",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_SignalType",
                table: "RiskSignals",
                column: "SignalType");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_Status",
                table: "RiskSignals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_UserId",
                table: "RiskSignals",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskSignals");
        }
    }
}
