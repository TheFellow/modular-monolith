using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Models;

namespace Mixology.Modules.Orders.Presentation;

public sealed class OrderActionProjector(IEntityAuthorizer authorizer)
{
    public static ActionId ListAction { get; } = new("orders.list");
    public static ActionId PlaceAction { get; } = new("orders.place");
    public static ActionId CompleteAction { get; } = new("orders.complete");
    public static ActionId CancelAction { get; } = new("orders.cancel");
    public static ActionId TagsAction { get; } = new("orders.tags");

    public Task<IReadOnlyList<ActionState>> ProjectAsync(
        Actor principal,
        Order? selected = null,
        CancellationToken cancellationToken = default)
    {
        ActionControl[] collection =
        [
            new(ListAction, ActionPermission.Public),
            new(PlaceAction, Require(principal, OrderAuthorization.Place, PlaceResource())),
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
                new ActionControl(
                    CompleteAction,
                    Require(principal, OrderAuthorization.Complete, resource),
                    [CompleteCondition(selected)]),
                new ActionControl(
                    CancelAction,
                    Require(principal, OrderAuthorization.Cancel, resource),
                    [CancelCondition(selected)]),
                new ActionControl(TagsAction, Require(principal, OrderAuthorization.Tag, resource)),
            ]),
            cancellationToken);
    }

    private ActionPermission Require(Actor principal, EntityUid action, Cedar.Types.Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));

    private static Cedar.Types.Entity PlaceResource() => new(
        new EntityUid(OrderAuthorization.ResourceType, string.Empty).ToCedarUid(),
        new Cedar.Types.EntityUidSet(),
        new Cedar.Types.CedarRecord(new Dictionary<Cedar.Types.CedarString, Cedar.Types.ICedarData>
        {
            [new Cedar.Types.CedarString("MenuID")] =
                new EntityUid(EntityIds.MenuType, string.Empty).ToCedarUid(),
            [new Cedar.Types.CedarString("Status")] = new Cedar.Types.CedarString(string.Empty),
        }),
        new Cedar.Types.CedarRecord());

    private static ActionCondition CompleteCondition(Order order) => _ =>
        ValueTask.FromResult(order.Status switch
        {
            var status when status == OrderStatus.Pending => ActionConditionResult.Enabled,
            var status when status == OrderStatus.Blocked => ActionConditionResult.Disabled(
                "Reserved stock is short; restock the blocked ingredients before completing this order."),
            var status when status == OrderStatus.Completed => ActionConditionResult.Disabled(
                "Available only while the order is pending; this order is completed."),
            var status when status == OrderStatus.Cancelled => ActionConditionResult.Disabled(
                "Available only while the order is pending; this order is cancelled."),
            _ => ActionConditionResult.Disabled("Available only while the order is pending."),
        });

    private static ActionCondition CancelCondition(Order order) => _ =>
        ValueTask.FromResult(order.Status switch
        {
            var status when status == OrderStatus.Pending || status == OrderStatus.Blocked =>
                ActionConditionResult.Enabled,
            var status when status == OrderStatus.Completed =>
                ActionConditionResult.Disabled("A completed order cannot be cancelled."),
            var status when status == OrderStatus.Cancelled =>
                ActionConditionResult.Disabled("This order is already cancelled."),
            _ => ActionConditionResult.Disabled("This order cannot be cancelled in its current state."),
        });
}
