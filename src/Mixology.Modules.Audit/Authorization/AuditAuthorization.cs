using Cedar.Types;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Audit.Models;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Audit.Authorization;

public static class AuditAuthorization
{
    public const string ResourceType = "Mixology::AuditEntry";
    public const string ActionType = "Mixology::AuditEntry::Action";

    public static KernelEntityUid List { get; } = new(ActionType, "list");
    public static KernelEntityUid Get { get; } = new(ActionType, "get");

    public static Entity ToCedarEntity(this AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return ToCedarEntity(entry.Id.EntityUid);
    }

    public static Entity ToCedarEntity(KernelEntityUid uid) =>
        new(
            new KernelEntityUid(ResourceType, uid.Id).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord());
}
