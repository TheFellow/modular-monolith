using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTagging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entity_tags",
                columns: table => new
                {
                    entity_type = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    key = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    value = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_tags", x => new { x.entity_type, x.entity_id, x.key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_tags_entity_type_entity_id",
                table: "entity_tags",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_tags_key_value",
                table: "entity_tags",
                columns: new[] { "key", "value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_tags");
        }
    }
}
