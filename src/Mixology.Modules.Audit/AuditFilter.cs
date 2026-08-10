using Mixology.Filtering;
using Mixology.Modules.Audit.Persistence;

namespace Mixology.Modules.Audit;

internal sealed record AuditFilter(
    string Id,
    string Action,
    string Resource,
    string Principal,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Success,
    string Error)
{
    public static FilterSchema<AuditFilter> Schema { get; } = new(
    [
        Filter.Field("id", (AuditFilter item) => item.Id, "Audit entry ID"),
        Filter.Field("action", (AuditFilter item) => item.Action, "Operation action"),
        Filter.Field("resource", (AuditFilter item) => item.Resource, "Primary resource"),
        Filter.Field("principal", (AuditFilter item) => item.Principal, "Actor"),
        Filter.Field("started_at", (AuditFilter item) => item.StartedAt, "Start time"),
        Filter.Field("completed_at", (AuditFilter item) => item.CompletedAt, "Completion time"),
        Filter.Field("success", (AuditFilter item) => item.Success, "Success status"),
        Filter.Field("error", (AuditFilter item) => item.Error, "Safe error detail"),
    ],
    "success && action.contains(\"Ingredient\")",
    "started_at >= date(\"2026-08-01T00:00:00Z\")",
    "!success && error.contains(\"conflict\")");

    public static FilterPersistenceMap<AuditEntryRow> Persistence { get; } = new(
    [
        Filter.PersistedField("id", (AuditEntryRow row) => row.Id),
        Filter.PersistedField("action", (AuditEntryRow row) => row.Action),
        Filter.PersistedField("success", (AuditEntryRow row) => row.Success),
        Filter.PersistedField("error", (AuditEntryRow row) => row.Error),
    ]);
}
