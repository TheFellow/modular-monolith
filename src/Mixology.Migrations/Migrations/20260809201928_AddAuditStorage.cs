using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    resource_type = table.Column<string>(type: "TEXT", nullable: true),
                    resource_id = table.Column<string>(type: "TEXT", nullable: true),
                    principal_id = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    error_kind = table.Column<int>(type: "INTEGER", nullable: true),
                    error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_touches",
                columns: table => new
                {
                    audit_entry_id = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_touches", x => new { x.audit_entry_id, x.position });
                    table.ForeignKey(
                        name: "FK_audit_touches_audit_entries_audit_entry_id",
                        column: x => x.audit_entry_id,
                        principalTable: "audit_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_action",
                table: "audit_entries",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_principal_id",
                table: "audit_entries",
                column: "principal_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_resource_type_resource_id",
                table: "audit_entries",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_started_at_utc",
                table: "audit_entries",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_success",
                table: "audit_entries",
                column: "success");

            migrationBuilder.CreateIndex(
                name: "IX_audit_touches_entity_type_entity_id",
                table: "audit_touches",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_touches");

            migrationBuilder.DropTable(
                name: "audit_entries");
        }
    }
}
