using Cedar.Types;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Drinks.Models;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Drinks.Authorization;

public static class DrinkAuthorization
{
    public const string ResourceType = "Mixology::Drink";
    public const string ActionType = "Mixology::Drink::Action";

    public static KernelEntityUid List { get; } = new(ActionType, "list");
    public static KernelEntityUid Get { get; } = new(ActionType, "get");
    public static KernelEntityUid Create { get; } = new(ActionType, "create");
    public static KernelEntityUid Update { get; } = new(ActionType, "update");
    public static KernelEntityUid Delete { get; } = new(ActionType, "delete");
    public static KernelEntityUid Tag { get; } = new(ActionType, "tag");
    public static KernelEntityUid Untag { get; } = new(ActionType, "untag");

    public static Entity ToCedarEntity(this Drink drink)
    {
        ArgumentNullException.ThrowIfNull(drink);
        Dictionary<CedarString, ICedarData> tags = drink.Tags.ToDictionary().ToDictionary(
            static pair => new CedarString(pair.Key),
            static pair => (ICedarData)new CedarString(pair.Value));
        Dictionary<CedarString, ICedarData> attributes = new()
        {
            [new CedarString("Name")] = new CedarString(drink.Name),
            [new CedarString("Category")] = new CedarString(drink.Category.Value),
            [new CedarString("Glass")] = new CedarString(drink.Glass.Value),
            [new CedarString("Description")] = new CedarString(drink.Description),
        };
        return new Entity(
            new KernelEntityUid(ResourceType, drink.Id.Value).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(attributes),
            new CedarRecord(tags));
    }
}
