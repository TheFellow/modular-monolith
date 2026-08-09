using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Models;

namespace Mixology.Modules.Ingredients.Presentation;

public sealed class IngredientActionProjector(IEntityAuthorizer authorizer)
{
    public static ActionId ListAction { get; } = new("ingredients.list");
    public static ActionId CreateAction { get; } = new("ingredients.create");
    public static ActionId EditAction { get; } = new("ingredients.edit");
    public static ActionId RetireAction { get; } = new("ingredients.retire");
    public static ActionId TagsAction { get; } = new("ingredients.tags");

    public Task<IReadOnlyList<ActionState>> ProjectAsync(
        Actor principal,
        Ingredient? selected = null,
        CancellationToken cancellationToken = default)
    {
        ActionControl[] collection =
        [
            new(ListAction, ActionPermission.Public),
            new(CreateAction, Require(principal, IngredientAuthorization.Create, CreateResource())),
        ];
        if (selected is null)
        {
            return ActionProjector.EvaluateAsync(new ActionGroup(Controls: collection), cancellationToken);
        }

        Cedar.Types.Entity resource = selected.ToCedarEntity();
        return ActionProjector.EvaluateAsync(
            new ActionGroup(Controls:
            [
                .. collection,
                new ActionControl(EditAction, Require(principal, IngredientAuthorization.Update, resource)),
                new ActionControl(RetireAction, Require(principal, IngredientAuthorization.Retire, resource)),
                new ActionControl(TagsAction, Require(principal, IngredientAuthorization.Tag, resource)),
            ]),
            cancellationToken);
    }

    private ActionPermission Require(Actor principal, EntityUid action, Cedar.Types.Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));

    private static Cedar.Types.Entity CreateResource() => new(
        new EntityUid(IngredientAuthorization.ResourceType, string.Empty).ToCedarUid(),
        new Cedar.Types.EntityUidSet(),
        new Cedar.Types.CedarRecord(new Dictionary<Cedar.Types.CedarString, Cedar.Types.ICedarData>
        {
            [new Cedar.Types.CedarString("Category")] = new Cedar.Types.CedarString(string.Empty),
            [new Cedar.Types.CedarString("Name")] = new Cedar.Types.CedarString(string.Empty),
            [new Cedar.Types.CedarString("Unit")] = new Cedar.Types.CedarString(string.Empty),
        }),
        new Cedar.Types.CedarRecord());
}
