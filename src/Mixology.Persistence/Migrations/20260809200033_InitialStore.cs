using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mixology.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialStore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "store_metadata",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_store_metadata", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "store_metadata");
    }
}
