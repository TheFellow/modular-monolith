using Mixology.Application.Authentication;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;

namespace Mixology.Modules.Audit.Models;

public sealed record AuditEntry(
    AuditEntryId Id,
    string Action,
    EntityUid? Resource,
    Actor Principal,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Success,
    ErrorKind? ErrorKind,
    string? Error,
    IReadOnlyList<EntityUid> Touches);
