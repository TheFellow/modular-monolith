using Cedar.Types;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Orders.Models;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Orders.Authorization;

public static class OrderAuthorization
{
    public const string ResourceType = "Mixology::Order";
    public const string ActionType = "Mixology::Order::Action";
    public static KernelEntityUid List { get; } = new(ActionType, "list");
    public static KernelEntityUid Get { get; } = new(ActionType, "get");
    public static KernelEntityUid Place { get; } = new(ActionType, "place");
    public static KernelEntityUid Complete { get; } = new(ActionType, "complete");
    public static KernelEntityUid Cancel { get; } = new(ActionType, "cancel");
    public static KernelEntityUid Tag { get; } = new(ActionType, "tag");
    public static KernelEntityUid Untag { get; } = new(ActionType, "untag");

    public static Entity ToCedarEntity(this Order order)
    {
        Dictionary<CedarString, ICedarData> attributes = new()
        {
            [new CedarString("MenuID")] = order.MenuId.EntityUid.ToCedarUid(),
            [new CedarString("Status")] = new CedarString(order.Status.Value),
        };
        Dictionary<CedarString, ICedarData> tags = order.Tags.ToDictionary().ToDictionary(
            static pair => new CedarString(pair.Key),
            static pair => (ICedarData)new CedarString(pair.Value));
        return new Entity(
            new KernelEntityUid(ResourceType, order.Id.Value).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(attributes),
            new CedarRecord(tags));
    }
}
