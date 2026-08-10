using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menus",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu_items",
                columns: table => new
                {
                    menu_id = table.Column<string>(type: "TEXT", nullable: false),
                    drink_id = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: true),
                    price_amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    price_currency = table.Column<string>(type: "TEXT", nullable: true),
                    featured = table.Column<bool>(type: "INTEGER", nullable: false),
                    availability = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => new { x.menu_id, x.drink_id });
                    table.ForeignKey(
                        name: "FK_menu_items_menus_menu_id",
                        column: x => x.menu_id,
                        principalTable: "menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_drink_id",
                table: "menu_items",
                column: "drink_id");

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_menu_id_sort_order",
                table: "menu_items",
                columns: new[] { "menu_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menus_created_at_utc",
                table: "menus",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_menus_name",
                table: "menus",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menus_status",
                table: "menus",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_items");

            migrationBuilder.DropTable(
                name: "menus");
        }
    }
}
