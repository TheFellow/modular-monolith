using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Drinks.Authorization;
using Mixology.Modules.Drinks.Models;

namespace Mixology.Modules.Drinks.Presentation;

public sealed class DrinkActionProjector(IEntityAuthorizer authorizer)
{
    public static ActionId ListAction { get; } = new("drinks.list");
    public static ActionId CreateAction { get; } = new("drinks.create");
    public static ActionId EditAction { get; } = new("drinks.edit");
    public static ActionId DeleteAction { get; } = new("drinks.delete");
    public static ActionId TagsAction { get; } = new("drinks.tags");

    public Task<IReadOnlyList<ActionState>> ProjectAsync(
        Actor principal,
        Drink? selected = null,
        CancellationToken cancellationToken = default)
    {
        ActionControl[] collection =
        [
            new(ListAction, ActionPermission.Public),
            new(CreateAction, Require(principal, DrinkAuthorization.Create, CreateResource())),
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
                new ActionControl(EditAction, Require(principal, DrinkAuthorization.Update, resource)),
                new ActionControl(DeleteAction, Require(principal, DrinkAuthorization.Delete, resource)),
                new ActionControl(TagsAction, Require(principal, DrinkAuthorization.Tag, resource)),
            ]),
            cancellationToken);
    }

    private ActionPermission Require(Actor principal, EntityUid action, Cedar.Types.Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));

    private static Cedar.Types.Entity CreateResource() => new(
        new EntityUid(DrinkAuthorization.ResourceType, string.Empty).ToCedarUid(),
        new Cedar.Types.EntityUidSet(),
        new Cedar.Types.CedarRecord(new Dictionary<Cedar.Types.CedarString, Cedar.Types.ICedarData>
        {
            [new Cedar.Types.CedarString("Name")] = new Cedar.Types.CedarString(string.Empty),
            [new Cedar.Types.CedarString("Category")] = new Cedar.Types.CedarString(string.Empty),
            [new Cedar.Types.CedarString("Glass")] = new Cedar.Types.CedarString(string.Empty),
            [new Cedar.Types.CedarString("Description")] = new Cedar.Types.CedarString(string.Empty),
        }),
        new Cedar.Types.CedarRecord());
}
