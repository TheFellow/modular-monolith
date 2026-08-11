using Mixology.Persistence;

namespace Mixology.Modules.Orders.Persistence;

internal sealed class OrderRow : IRevisionedRow
{
    public required string Id { get; init; }
    public required string MenuId { get; init; }
    public required string Status { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
    public required string Notes { get; init; }
    public DateTime? DeletedAtUtc { get; set; }
    public long Revision { get; set; }
    public List<OrderItemRow> Items { get; } = [];
    public List<OrderIngredientUsageRow> IngredientUsage { get; } = [];
    public List<OrderBlockedIngredientRow> BlockedIngredients { get; } = [];
}

internal sealed class OrderItemRow
{
    public required string OrderId { get; init; }
    public int Position { get; init; }
    public required string DrinkId { get; init; }
    public int Quantity { get; init; }
    public required string Notes { get; init; }
}

internal sealed class OrderIngredientUsageRow
{
    public required string OrderId { get; init; }
    public int Position { get; init; }
    public required string IngredientId { get; init; }
    public required string Name { get; init; }
    public double Quantity { get; init; }
    public required string Unit { get; init; }
}

internal sealed class OrderBlockedIngredientRow
{
    public required string OrderId { get; init; }
    public required string IngredientId { get; init; }
}
