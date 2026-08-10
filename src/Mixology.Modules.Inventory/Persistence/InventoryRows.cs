namespace Mixology.Modules.Inventory.Persistence;

internal sealed class InventoryRow
{
    public required string Id { get; init; }
    public required string IngredientId { get; init; }
    public double Quantity { get; set; }
    public required string Unit { get; set; }
    public decimal? UnitCostAmount { get; set; }
    public string? UnitCostCurrency { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}

internal sealed class InventoryReservationRow
{
    public required string Id { get; init; }
    public required string OrderId { get; init; }
    public required string IngredientId { get; init; }
    public double Quantity { get; set; }
    public required string Unit { get; set; }
}
