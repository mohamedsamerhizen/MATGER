using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MATGER.Api.Migrations
{
    /// <inheritdoc />
    public partial class Customer360InternalNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerInternalNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsImportant = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInternalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInternalNotes_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerInternalNotes_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInternalNotes_CreatedAtUtc",
                table: "CustomerInternalNotes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInternalNotes_CreatedByUserId",
                table: "CustomerInternalNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInternalNotes_CustomerId",
                table: "CustomerInternalNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInternalNotes_IsImportant",
                table: "CustomerInternalNotes",
                column: "IsImportant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerInternalNotes");
        }
    }
}
