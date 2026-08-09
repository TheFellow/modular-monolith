using Cedar.Types;
using Mixology.Authorization.Cedar;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Tagging.Authorization;

public static class TaggingAuthorization
{
    public const string ResourceType = "Mixology::TagDiscovery";
    public const string ActionType = "Mixology::TagDiscovery::Action";

    public static KernelEntityUid Show { get; } = new(ActionType, "show");
    public static KernelEntityUid Summary { get; } = new(ActionType, "summary");

    public static Entity DiscoveryResource(string id, string key = "", string value = "", bool exact = false)
    {
        Dictionary<CedarString, ICedarData> attributes = new()
        {
            [new CedarString("Key")] = new CedarString(key),
            [new CedarString("Value")] = new CedarString(value),
            [new CedarString("Exact")] = new CedarBool(exact),
        };
        return new Entity(
            new KernelEntityUid(ResourceType, id).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(attributes),
            new CedarRecord());
    }
}
