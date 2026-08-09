using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Inventory.Tests.Persistence;

public sealed class InventoryModelConfigurationTests
{
    [Fact]
    public void ConfigurationOwnsStockAndReservationSchemas()
    {
        DbContextOptions<MixologyDbContext> options = new DbContextOptionsBuilder<MixologyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using MixologyDbContext context = new(options, [new InventoryModelConfiguration()]);
        IEntityType stock = Assert.Single(
            context.Model.GetEntityTypes(),
            entity => entity.GetTableName() == "inventory_stock");
        IEntityType reservation = Assert.Single(
            context.Model.GetEntityTypes(),
            entity => entity.GetTableName() == "inventory_reservations");

        Assert.Contains("InventoryRow", stock.Name, StringComparison.Ordinal);
        Assert.Contains("InventoryReservationRow", reservation.Name, StringComparison.Ordinal);
        Assert.True(Assert.Single(
            stock.GetIndexes(),
            index => index.Properties.Single().Name == "IngredientId").IsUnique);
        Assert.Single(reservation.GetForeignKeys());
        Assert.Contains(
            reservation.GetIndexes(),
            index => index.Properties.Single().Name == "OrderId");
        AssertColumn(stock, "Quantity", "quantity", nullable: false);
        AssertColumn(stock, "UnitCostAmount", "unit_cost_amount", nullable: true);
        AssertColumn(stock, "UnitCostCurrency", "unit_cost_currency", nullable: true);
    }

    private static void AssertColumn(IEntityType entity, string propertyName, string columnName, bool nullable)
    {
        IProperty property = entity.FindProperty(propertyName)!;
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(nullable, property.IsNullable);
    }
}
