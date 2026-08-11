using Mixology.Persistence;

namespace Mixology.Modules.Tagging.Persistence;

internal sealed class TagAssociationRow : IRevisionedRow
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Key { get; init; }
    public required string Value { get; set; }
    public long Revision { get; set; }
}
