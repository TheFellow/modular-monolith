using Cedar.Types;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Inventory.Models;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Inventory.Authorization;

public static class InventoryAuthorization
{
    public const string ResourceType = "Mixology::Inventory";
    public const string ActionType = "Mixology::Inventory::Action";

    public static KernelEntityUid List { get; } = new(ActionType, "list");
    public static KernelEntityUid Get { get; } = new(ActionType, "get");
    public static KernelEntityUid Adjust { get; } = new(ActionType, "adjust");
    public static KernelEntityUid Set { get; } = new(ActionType, "set");
    public static KernelEntityUid Tag { get; } = new(ActionType, "tag");
    public static KernelEntityUid Untag { get; } = new(ActionType, "untag");

    public static Entity ToCedarEntity(this InventoryStock inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return new InventoryAuthorizationResource(
            inventory.EntityUid,
            inventory.IngredientId,
            inventory.OnHand.Unit.Value,
            inventory.Tags.ToDictionary()).ToCedarEntity();
    }

    public static Entity ToCedarEntity(this InventoryAuthorizationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Dictionary<CedarString, ICedarData> tags = resource.Tags.ToDictionary(
            static pair => new CedarString(pair.Key),
            static pair => (ICedarData)new CedarString(pair.Value));
        Dictionary<CedarString, ICedarData> attributes = new()
        {
            [new CedarString("IngredientID")] = resource.IngredientId.EntityUid.ToCedarUid(),
            [new CedarString("Unit")] = new CedarString(resource.Unit),
        };
        return new Entity(
            new KernelEntityUid(ResourceType, resource.Uid.Id).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(attributes),
            new CedarRecord(tags));
    }
}

public sealed record InventoryAuthorizationResource(
    KernelEntityUid Uid,
    IngredientId IngredientId,
    string Unit,
    IReadOnlyDictionary<string, string> Tags);
