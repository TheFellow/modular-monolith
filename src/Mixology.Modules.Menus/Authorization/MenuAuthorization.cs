using Cedar.Types;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Menus.Models;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Menus.Authorization;

public static class MenuAuthorization
{
    public const string ResourceType = "Mixology::Menu";
    public const string ActionType = "Mixology::Menu::Action";

    public static KernelEntityUid List { get; } = new(ActionType, "list");
    public static KernelEntityUid Get { get; } = new(ActionType, "get");
    public static KernelEntityUid Readiness { get; } = new(ActionType, "readiness");
    public static KernelEntityUid Create { get; } = new(ActionType, "create");
    public static KernelEntityUid Update { get; } = new(ActionType, "update");
    public static KernelEntityUid Delete { get; } = new(ActionType, "delete");
    public static KernelEntityUid AddDrink { get; } = new(ActionType, "add_drink");
    public static KernelEntityUid RemoveDrink { get; } = new(ActionType, "remove_drink");
    public static KernelEntityUid Publish { get; } = new(ActionType, "publish");
    public static KernelEntityUid Draft { get; } = new(ActionType, "draft");
    public static KernelEntityUid Tag { get; } = new(ActionType, "tag");
    public static KernelEntityUid Untag { get; } = new(ActionType, "untag");

    public static Entity ToCedarEntity(this Menu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return new MenuAuthorizationResource(
            menu.EntityUid,
            menu.Tags.ToDictionary(),
            menu.Name,
            menu.Status.Value).ToCedarEntity();
    }

    public static Entity ToCedarEntity(this MenuAuthorizationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Dictionary<CedarString, ICedarData> tags = resource.Tags.ToDictionary(
            static pair => new CedarString(pair.Key),
            static pair => (ICedarData)new CedarString(pair.Value));
        Dictionary<CedarString, ICedarData> attributes = new()
        {
            [new CedarString("Name")] = new CedarString(resource.Name),
            [new CedarString("Status")] = new CedarString(resource.Status),
        };
        return new Entity(
            new KernelEntityUid(ResourceType, resource.Uid.Id).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(attributes),
            new CedarRecord(tags));
    }
}

public sealed record MenuAuthorizationResource(
    KernelEntityUid Uid,
    IReadOnlyDictionary<string, string> Tags,
    string Name,
    string Status);
