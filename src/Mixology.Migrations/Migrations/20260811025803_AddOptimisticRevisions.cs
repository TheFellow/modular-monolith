using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimisticRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "menus",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "inventory_stock",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "inventory_reservations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "ingredients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "entity_tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "drinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "revision",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "menus");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "inventory_stock");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "inventory_reservations");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "entity_tags");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "drinks");
        }
    }
}
