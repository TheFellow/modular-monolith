using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Inventory.Authorization;
using Mixology.Modules.Inventory.Models;

namespace Mixology.Modules.Inventory.Presentation;

public sealed class InventoryActionProjector(IEntityAuthorizer authorizer)
{
    public static ActionId ListAction { get; } = new("inventory.list");
    public static ActionId AdjustAction { get; } = new("inventory.adjust");
    public static ActionId SetAction { get; } = new("inventory.set");
    public static ActionId TagsAction { get; } = new("inventory.tags");

    public Task<IReadOnlyList<ActionState>> ProjectAsync(
        Actor principal,
        InventoryStock? selected = null,
        CancellationToken cancellationToken = default)
    {
        ActionControl list = new(ListAction, ActionPermission.Public);
        if (selected is null)
        {
            return ActionProjector.EvaluateAsync(new ActionGroup(Controls: [list]), cancellationToken);
        }

        Cedar.Types.Entity resource = selected.ToCedarEntity();
        return ActionProjector.EvaluateAsync(
            new ActionGroup(Controls:
            [
                list,
                new ActionControl(AdjustAction, Require(principal, InventoryAuthorization.Adjust, resource)),
                new ActionControl(SetAction, Require(principal, InventoryAuthorization.Set, resource)),
                new ActionControl(TagsAction, Require(principal, InventoryAuthorization.Tag, resource)),
            ]),
            cancellationToken);
    }

    private ActionPermission Require(Actor principal, EntityUid action, Cedar.Types.Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));
}
