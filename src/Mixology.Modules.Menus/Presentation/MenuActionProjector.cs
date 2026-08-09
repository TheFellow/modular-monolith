using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Modules.Menus.Authorization;
using Mixology.Modules.Menus.Models;

namespace Mixology.Modules.Menus.Presentation;

public sealed class MenuActionProjector(IEntityAuthorizer authorizer)
{
    public static ActionId ListAction { get; } = new("menus.list");
    public static ActionId CreateAction { get; } = new("menus.create");
    public static ActionId EditAction { get; } = new("menus.edit");
    public static ActionId DeleteAction { get; } = new("menus.delete");
    public static ActionId TagsAction { get; } = new("menus.tags");
    public static ActionId AddDrinkAction { get; } = new("menus.drink.add");
    public static ActionId RemoveDrinkAction { get; } = new("menus.drink.remove");
    public static ActionId PublishAction { get; } = new("menus.publish");
    public static ActionId DraftAction { get; } = new("menus.draft");
    public static ActionId ReadinessAction { get; } = new("menus.readiness");

    public async Task<IReadOnlyList<ActionState>> ProjectAsync(
        Actor principal,
        Menu? selected = null,
        CancellationToken cancellationToken = default)
    {
        ActionControl[] collection =
        [
            new(ListAction, ActionPermission.Public),
            new(CreateAction, Require(principal, MenuAuthorization.Create, CreateResource())),
        ];
        if (selected is null)
        {
            return await ActionProjector.EvaluateAsync(
                new ActionGroup(Controls: collection),
                cancellationToken).ConfigureAwait(false);
        }

        Cedar.Types.Entity resource = selected.ToCedarEntity();
        ActionPermission update = Require(principal, MenuAuthorization.Update, resource);
        ActionCondition draftOnly = LifecycleCondition(
            selected.RequireDraft,
            "Available only while the menu is a draft.");
        ActionGroup declaration = new(
            Controls:
            [
                .. collection,
                new ActionControl(
                    ReadinessAction,
                    Require(principal, MenuAuthorization.Readiness, resource)),
            ],
            Groups:
            [
                new ActionGroup(
                    update,
                    [
                        new ActionControl(EditAction, Conditions: [draftOnly]),
                        new ActionControl(
                            DeleteAction,
                            Require(principal, MenuAuthorization.Delete, resource),
                            [draftOnly]),
                        new ActionControl(
                            TagsAction,
                            Require(principal, MenuAuthorization.Tag, resource)),
                        new ActionControl(
                            AddDrinkAction,
                            Require(principal, MenuAuthorization.AddDrink, resource),
                            [draftOnly]),
                        new ActionControl(
                            RemoveDrinkAction,
                            Require(principal, MenuAuthorization.RemoveDrink, resource),
                            [draftOnly, HasDrinkCondition(selected)]),
                        new ActionControl(
                            PublishAction,
                            Require(principal, MenuAuthorization.Publish, resource),
                            [PublishCondition(selected)]),
                        new ActionControl(
                            DraftAction,
                            Require(principal, MenuAuthorization.Draft, resource),
                            [LifecycleCondition(
                                selected.RequireReturnToDraft,
                                "Available only while the menu is published.")]),
                    ]),
            ]);
        return await ActionProjector.EvaluateAsync(declaration, cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<ActionState> ApplyReadiness(
        IReadOnlyList<ActionState> states,
        ReadinessReport report)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(report);
        if (!report.HasBlockers)
        {
            return states.ToArray();
        }

        return states.Select(state =>
            state.Id == PublishAction && state.Visible && state.Enabled
                ? state with
                {
                    Enabled = false,
                    DisabledReason = "Resolve menu readiness blockers before publishing.",
                }
                : state).ToArray();
    }

    private ActionPermission Require(Actor principal, EntityUid action, Cedar.Types.Entity resource) =>
        ActionPermission.Require(token => authorizer.AuthorizeAsync(principal, action, resource, token));

    private static Cedar.Types.Entity CreateResource() => new(
        new EntityUid(MenuAuthorization.ResourceType, string.Empty).ToCedarUid(),
        new Cedar.Types.EntityUidSet(),
        new Cedar.Types.CedarRecord(new Dictionary<Cedar.Types.CedarString, Cedar.Types.ICedarData>
        {
            [new Cedar.Types.CedarString("Name")] = new Cedar.Types.CedarString(string.Empty),
            [new Cedar.Types.CedarString("Status")] = new Cedar.Types.CedarString(string.Empty),
        }),
        new Cedar.Types.CedarRecord());

    private static ActionCondition LifecycleCondition(Action require, string reason) => _ =>
        ValueTask.FromResult(Satisfies(require) ? ActionConditionResult.Enabled : ActionConditionResult.Disabled(reason));

    private static ActionCondition HasDrinkCondition(Menu menu) => _ =>
        ValueTask.FromResult(menu.Items.Count > 0
            ? ActionConditionResult.Enabled
            : ActionConditionResult.Disabled("Add a drink before trying to remove one."));

    private static ActionCondition PublishCondition(Menu menu) => _ =>
        ValueTask.FromResult(Satisfies(menu.RequirePublishable)
            ? ActionConditionResult.Enabled
            : !Satisfies(menu.RequireDraft)
                ? ActionConditionResult.Disabled("Available only while the menu is a draft.")
                : ActionConditionResult.Disabled("Add at least one drink before publishing."));

    private static bool Satisfies(Action require)
    {
        try
        {
            require();
            return true;
        }
        catch (Exception exception) when (Mixology.Kernel.Errors.AppError.IsFailedPrecondition(exception))
        {
            return false;
        }
    }
}
