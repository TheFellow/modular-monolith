namespace Mixology.Modules.Tagging.Persistence;

internal sealed class TagAssociationRow
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Key { get; init; }
    public required string Value { get; set; }
}
