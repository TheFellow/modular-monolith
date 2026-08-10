using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_stock",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ingredient_id = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false),
                    unit_cost_amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    unit_cost_currency = table.Column<string>(type: "TEXT", nullable: true),
                    last_updated_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_stock", x => x.id);
                    table.UniqueConstraint("AK_inventory_stock_ingredient_id", x => x.ingredient_id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    order_id = table.Column<string>(type: "TEXT", nullable: false),
                    ingredient_id = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_inventory_stock_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "inventory_stock",
                        principalColumn: "ingredient_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ingredient_id",
                table: "inventory_reservations",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_order_id",
                table: "inventory_reservations",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_ingredient_id",
                table: "inventory_stock",
                column: "ingredient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_last_updated_utc",
                table: "inventory_stock",
                column: "last_updated_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_reservations");

            migrationBuilder.DropTable(
                name: "inventory_stock");
        }
    }
}
