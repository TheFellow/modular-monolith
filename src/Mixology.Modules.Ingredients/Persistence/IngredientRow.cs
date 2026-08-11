using Mixology.Persistence;

namespace Mixology.Modules.Ingredients.Persistence;

internal sealed class IngredientRow : IRevisionedRow
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Unit { get; set; }
    public required string Description { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long Revision { get; set; }
}
