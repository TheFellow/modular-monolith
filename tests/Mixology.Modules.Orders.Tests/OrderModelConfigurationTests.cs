using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mixology.Modules.Orders.Persistence;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Orders.Tests;

public sealed class OrderModelConfigurationTests
{
    [Fact]
    public void ConfigurationOwnsOrderAndImmutableSnapshotTables()
    {
        DbContextOptions<MixologyDbContext> options = new DbContextOptionsBuilder<MixologyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using MixologyDbContext context = new(options, [new OrderModelConfiguration()]);

        IEntityType order = Table(context, "orders");
        IEntityType item = Table(context, "order_items");
        IEntityType usage = Table(context, "order_ingredient_usage");
        IEntityType blocked = Table(context, "order_blocked_ingredients");

        Assert.Equal(3, order.GetReferencingForeignKeys().Count());
        Assert.Equal(2, item.FindPrimaryKey()!.Properties.Count);
        Assert.Equal(2, usage.FindPrimaryKey()!.Properties.Count);
        Assert.Equal(2, blocked.FindPrimaryKey()!.Properties.Count);
        Assert.Equal("completed_at_utc", order.FindProperty("CompletedAtUtc")!.GetColumnName());
        Assert.Equal("quantity", usage.FindProperty("Quantity")!.GetColumnName());
    }

    private static IEntityType Table(MixologyDbContext context, string name) =>
        Assert.Single(context.Model.GetEntityTypes(), entity => entity.GetTableName() == name);
}
