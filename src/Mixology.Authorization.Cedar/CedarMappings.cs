using Cedar.Types;
using Mixology.Application.Authentication;

namespace Mixology.Authorization.Cedar;

public static class CedarMappings
{
    public const string ActorType = "Mixology::Actor";

    public static EntityUid ToCedarUid(this Mixology.Kernel.Entities.EntityUid uid) =>
        new(new EntityType(uid.Type), new CedarString(uid.Id));

    public static EntityUid ToCedarUid(this Actor actor) =>
        new(new EntityType(ActorType), new CedarString(actor.Id));

    public static Entity ToCedarEntity(this Actor actor) =>
        new(actor.ToCedarUid(), new EntityUidSet(), new CedarRecord(), new CedarRecord());
}
