using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    menu_id = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_blocked_ingredients",
                columns: table => new
                {
                    order_id = table.Column<string>(type: "TEXT", nullable: false),
                    ingredient_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_blocked_ingredients", x => new { x.order_id, x.ingredient_id });
                    table.ForeignKey(
                        name: "FK_order_blocked_ingredients_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_ingredient_usage",
                columns: table => new
                {
                    order_id = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    ingredient_id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_ingredient_usage", x => new { x.order_id, x.position });
                    table.ForeignKey(
                        name: "FK_order_ingredient_usage_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    order_id = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    drink_id = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => new { x.order_id, x.position });
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_blocked_ingredients_ingredient_id",
                table: "order_blocked_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_ingredient_usage_ingredient_id",
                table: "order_ingredient_usage",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_drink_id",
                table: "order_items",
                column: "drink_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_created_at_utc",
                table: "orders",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_orders_menu_id",
                table: "orders",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_status",
                table: "orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_blocked_ingredients");

            migrationBuilder.DropTable(
                name: "order_ingredient_usage");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
