using Mixology.Persistence;

namespace Mixology.Modules.Menus.Persistence;

internal sealed class MenuRow : IRevisionedRow
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Status { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long Revision { get; set; }
    public List<MenuItemRow> Items { get; } = [];
}

internal sealed class MenuItemRow
{
    public required string MenuId { get; init; }
    public required string DrinkId { get; init; }
    public string? DisplayName { get; set; }
    public decimal? PriceAmount { get; set; }
    public string? PriceCurrency { get; set; }
    public required bool Featured { get; set; }
    public required string Availability { get; set; }
    public required int SortOrder { get; set; }
}
