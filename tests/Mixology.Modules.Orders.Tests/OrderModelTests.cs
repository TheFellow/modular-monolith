using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Modules.Orders.Models;
using Xunit;

namespace Mixology.Modules.Orders.Tests;

public sealed class OrderModelTests
{
    [Fact]
    public void NormalizeOwnsAndOrdersFulfillmentAndBlockedSnapshots()
    {
        IngredientId first = IngredientId.New();
        IngredientId second = IngredientId.New();
        IngredientUsage later = new(second, "  Lime  ", Amount.Create(2d, Unit.Piece));
        IngredientUsage earlier = new(first, "Gin", Amount.Create(50d, Unit.Milliliter));
        Order order = Order(
            OrderStatus.Blocked,
            [later, earlier],
            [second, first, second]).Normalize();

        IngredientId[] expected = new[] { first, second }
            .OrderBy(static value => value.Value, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, order.IngredientUsage.Select(static value => value.IngredientId));
        Assert.Equal(expected, order.BlockedIngredientIds);
        Assert.Equal("Lime", Assert.Single(
            order.IngredientUsage,
            value => value.IngredientId == second).Name);
    }

    [Fact]
    public void CompletionTimestampBelongsOnlyToCompletedState()
    {
        Assert.Throws<InvalidError>(() => Order(OrderStatus.Completed, [], []).Normalize());
        Assert.Throws<InvalidError>(() => (Order(OrderStatus.Pending, [], []) with
        {
            CompletedAt = DateTimeOffset.UtcNow,
        }).Normalize());

        Order completed = (Order(OrderStatus.Completed, [], []) with
        {
            CompletedAt = DateTimeOffset.UtcNow,
        }).Normalize();
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public void ItemsRequirePositiveQuantityAndIdentity()
    {
        Assert.Throws<InvalidError>(() => new OrderItem(default, 1, string.Empty).Normalize());
        Assert.Throws<InvalidError>(() => new OrderItem(DrinkId.New(), 0, string.Empty).Normalize());
    }

    private static Order Order(
        OrderStatus status,
        IReadOnlyList<IngredientUsage> usage,
        IReadOnlyList<IngredientId> blocked) => new(
        OrderId.New(),
        MenuId.New(),
        [new OrderItem(DrinkId.New(), 1, string.Empty)],
        usage,
        blocked,
        status,
        DateTimeOffset.UtcNow,
        null,
        string.Empty,
        null,
        TagCollection.Empty);
}
