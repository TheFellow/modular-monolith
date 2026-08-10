using Cedar.Types;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Ingredients.Models;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Ingredients.Authorization;

public static class IngredientAuthorization
{
    public const string ResourceType = "Mixology::Ingredient";
    public const string ActionType = "Mixology::Ingredient::Action";

    public static KernelEntityUid List { get; } = new(ActionType, "list");
    public static KernelEntityUid Get { get; } = new(ActionType, "get");
    public static KernelEntityUid Create { get; } = new(ActionType, "create");
    public static KernelEntityUid Update { get; } = new(ActionType, "update");
    public static KernelEntityUid Retire { get; } = new(ActionType, "retire");
    public static KernelEntityUid Tag { get; } = new(ActionType, "tag");
    public static KernelEntityUid Untag { get; } = new(ActionType, "untag");

    public static Entity ToCedarEntity(this Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);

        return new IngredientAuthorizationResource(
            ingredient.EntityUid,
            ingredient.Tags.ToDictionary(),
            ingredient.Category.Value,
            ingredient.Name,
            ingredient.Unit.Value).ToCedarEntity();
    }

    public static Entity ToCedarEntity(this IngredientAuthorizationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        Dictionary<CedarString, ICedarData> tags = resource.Tags.ToDictionary(
            static pair => new CedarString(pair.Key),
            static pair => (ICedarData)new CedarString(pair.Value));

        Dictionary<CedarString, ICedarData> attributes = new()
        {
            [new CedarString("Category")] = new CedarString(resource.Category),
            [new CedarString("Name")] = new CedarString(resource.Name),
            [new CedarString("Unit")] = new CedarString(resource.Unit),
        };

        return new Entity(
            new KernelEntityUid(ResourceType, resource.Uid.Id).ToCedarUid(),
            new EntityUidSet(),
            new CedarRecord(attributes),
            new CedarRecord(tags));
    }
}

public sealed record IngredientAuthorizationResource(
    KernelEntityUid Uid,
    IReadOnlyDictionary<string, string> Tags,
    string Category,
    string Name,
    string Unit);
