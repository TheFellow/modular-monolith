using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Errors;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Tagging.Presentation;

namespace Mixology.Presentation.Navigation;

public readonly record struct WorkspaceId(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}

public sealed record NavigationItem(WorkspaceId Id, string Label);

public sealed record NavigationProjection(
    IReadOnlyList<NavigationItem> Items,
    IReadOnlyList<Exception> Errors);

public sealed class NavigationProjector(
    DrinkActionProjector drinks,
    IngredientActionProjector ingredients,
    InventoryActionProjector inventory,
    MenuActionProjector menus,
    OrderActionProjector orders,
    AuditActionProjector audit,
    TaggingActionProjector tagging)
{
    public static WorkspaceId DashboardWorkspace { get; } = new("dashboard");
    public static WorkspaceId DrinksWorkspace { get; } = new("drinks");
    public static WorkspaceId IngredientsWorkspace { get; } = new("ingredients");
    public static WorkspaceId InventoryWorkspace { get; } = new("inventory");
    public static WorkspaceId MenusWorkspace { get; } = new("menus");
    public static WorkspaceId OrdersWorkspace { get; } = new("orders");
    public static WorkspaceId AuditWorkspace { get; } = new("audit");
    public static WorkspaceId TagsWorkspace { get; } = new("tags");

    public async Task<NavigationProjection> ProjectAsync(
        Actor principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.IsEmpty)
        {
            principal = Actor.Anonymous;
        }

        List<NavigationItem> items = [new(DashboardWorkspace, "Dashboard")];
        List<Exception> errors = [];
        await IncludeAsync(
            DrinksWorkspace,
            "Drinks",
            DrinkActionProjector.ListAction,
            () => drinks.ProjectAsync(principal, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await IncludeAsync(
            IngredientsWorkspace,
            "Ingredients",
            IngredientActionProjector.ListAction,
            () => ingredients.ProjectAsync(principal, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await IncludeAsync(
            InventoryWorkspace,
            "Inventory",
            InventoryActionProjector.ListAction,
            () => inventory.ProjectAsync(principal, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await IncludeAsync(
            MenusWorkspace,
            "Menus",
            MenuActionProjector.ListAction,
            () => menus.ProjectAsync(principal, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await IncludeAsync(
            OrdersWorkspace,
            "Orders",
            OrderActionProjector.ListAction,
            () => orders.ProjectAsync(principal, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await IncludeAsync(
            AuditWorkspace,
            "Audit",
            AuditActionProjector.ListAction,
            () => audit.ProjectAsync(principal, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await IncludeAsync(
            TagsWorkspace,
            "Tags",
            TaggingActionProjector.SummaryAction,
            () => tagging.ProjectDiscoveryAsync(principal, cancellationToken)).ConfigureAwait(false);
        return new NavigationProjection(items, errors);

        async Task IncludeAsync(
            WorkspaceId workspace,
            string label,
            ActionId capability,
            Func<Task<IReadOnlyList<ActionState>>> project)
        {
            try
            {
                IReadOnlyList<ActionState> states = await project().ConfigureAwait(false);
                ActionState? state = states.SingleOrDefault(candidate => candidate.Id == capability);
                if (state is null)
                {
                    throw AppError.Internal($"capability projection omitted {capability}");
                }

                if (state.Visible)
                {
                    items.Add(new NavigationItem(workspace, label));
                }
            }
            catch (Exception exception) when (
                !AppError.IsPermission(exception) && !AppError.IsCancellation(exception))
            {
                errors.Add(AppError.Find(exception)
                    ?? AppError.Internal($"project navigation workspace {workspace}", exception));
                items.Add(new NavigationItem(workspace, label));
            }
        }
    }
}
