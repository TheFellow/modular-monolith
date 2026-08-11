using Mixology.Persistence;

namespace Mixology.Modules.Inventory.Persistence;

internal sealed class InventoryRow : IRevisionedRow
{
    public required string Id { get; init; }
    public required string IngredientId { get; init; }
    public double Quantity { get; set; }
    public required string Unit { get; set; }
    public decimal? UnitCostAmount { get; set; }
    public string? UnitCostCurrency { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public long Revision { get; set; }
}

internal sealed class InventoryReservationRow : IRevisionedRow
{
    public required string Id { get; init; }
    public required string OrderId { get; init; }
    public required string IngredientId { get; init; }
    public double Quantity { get; set; }
    public required string Unit { get; set; }
    public long Revision { get; set; }
}
