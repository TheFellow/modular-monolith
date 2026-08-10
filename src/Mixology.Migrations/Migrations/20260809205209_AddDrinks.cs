using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDrinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drinks",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", nullable: false),
                    glass = table.Column<string>(type: "TEXT", nullable: false),
                    garnish = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drinks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drink_recipe_ingredients",
                columns: table => new
                {
                    drink_id = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    ingredient_id = table.Column<string>(type: "TEXT", nullable: false),
                    amount = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false),
                    optional = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drink_recipe_ingredients", x => new { x.drink_id, x.position });
                    table.ForeignKey(
                        name: "FK_drink_recipe_ingredients_drinks_drink_id",
                        column: x => x.drink_id,
                        principalTable: "drinks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "drink_recipe_steps",
                columns: table => new
                {
                    drink_id = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drink_recipe_steps", x => new { x.drink_id, x.position });
                    table.ForeignKey(
                        name: "FK_drink_recipe_steps_drinks_drink_id",
                        column: x => x.drink_id,
                        principalTable: "drinks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "drink_recipe_substitutes",
                columns: table => new
                {
                    drink_id = table.Column<string>(type: "TEXT", nullable: false),
                    ingredient_position = table.Column<int>(type: "INTEGER", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    substitute_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drink_recipe_substitutes", x => new { x.drink_id, x.ingredient_position, x.position });
                    table.ForeignKey(
                        name: "FK_drink_recipe_substitutes_drink_recipe_ingredients_drink_id_ingredient_position",
                        columns: x => new { x.drink_id, x.ingredient_position },
                        principalTable: "drink_recipe_ingredients",
                        principalColumns: new[] { "drink_id", "position" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_drink_recipe_ingredients_ingredient_id",
                table: "drink_recipe_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_drink_recipe_substitutes_substitute_id",
                table: "drink_recipe_substitutes",
                column: "substitute_id");

            migrationBuilder.CreateIndex(
                name: "IX_drinks_category",
                table: "drinks",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_drinks_glass",
                table: "drinks",
                column: "glass");

            migrationBuilder.CreateIndex(
                name: "IX_drinks_name",
                table: "drinks",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drinks_status",
                table: "drinks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drink_recipe_steps");

            migrationBuilder.DropTable(
                name: "drink_recipe_substitutes");

            migrationBuilder.DropTable(
                name: "drink_recipe_ingredients");

            migrationBuilder.DropTable(
                name: "drinks");
        }
    }
}
