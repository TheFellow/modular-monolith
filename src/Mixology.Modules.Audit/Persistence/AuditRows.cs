namespace Mixology.Modules.Audit.Persistence;

internal sealed class AuditEntryRow
{
    public required string Id { get; init; }
    public required string Action { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public required string PrincipalId { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
    public required bool Success { get; init; }
    public int? ErrorKind { get; init; }
    public string? Error { get; init; }
    public List<AuditTouchRow> Touches { get; init; } = [];
}

internal sealed class AuditTouchRow
{
    public required string AuditEntryId { get; init; }
    public required int Position { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
}
