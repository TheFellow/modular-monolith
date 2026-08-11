using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Gui.Navigation;
using Mixology.Gui.Workspaces;
using Mixology.Gui.Workspaces.Audit;
using Mixology.Gui.Workspaces.Dashboard;
using Mixology.Gui.Workspaces.Drinks;
using Mixology.Gui.Workspaces.Ingredients;
using Mixology.Gui.Workspaces.Inventory;
using Mixology.Gui.Workspaces.Menus;
using Mixology.Gui.Workspaces.Orders;
using Mixology.Gui.Workspaces.Tags;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Presentation;
using Mixology.Persistence;
using Mixology.Presentation.Dashboard;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Gui;

public static class DesktopShellFactory
{
    public static async Task<ShellViewModel> CreateAsync(
        IServiceProvider services,
        Actor actor,
        IDirtyNavigationConfirmation? confirmation = null,
        IUiDispatcher? dispatcher = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        dispatcher ??= new ImmediateUiDispatcher();
        MixologySession session = services.GetRequiredService<MixologySessionFactory>().Create(actor);
        NavigationProjection navigation = await services.GetRequiredService<NavigationProjector>()
            .ProjectAsync(actor, cancellationToken).ConfigureAwait(false);

        AuditModule audit = services.GetRequiredService<AuditModule>();
        DrinksModule drinks = services.GetRequiredService<DrinksModule>();
        IngredientsModule ingredients = services.GetRequiredService<IngredientsModule>();
        InventoryModule inventory = services.GetRequiredService<InventoryModule>();
        MenusModule menus = services.GetRequiredService<MenusModule>();
        OrdersModule orders = services.GetRequiredService<OrdersModule>();
        TaggingModule tagging = services.GetRequiredService<TaggingModule>();
        TaggedMutationCoordinator taggedMutations = services.GetRequiredService<TaggedMutationCoordinator>();

        IReadOnlyDictionary<WorkspaceId, Func<IDesktopWorkspace>> workspaces =
            new Dictionary<WorkspaceId, Func<IDesktopWorkspace>>
            {
                [NavigationProjector.DashboardWorkspace] = () => new DashboardViewModel(
                    token => services.GetRequiredService<DashboardService>().LoadAsync(session, token),
                    dispatcher),
                [NavigationProjector.DrinksWorkspace] = DrinksWorkspaceViewModel.CreateFactory(
                    drinks,
                    ingredients,
                    services.GetRequiredService<DrinkActionProjector>(),
                    taggedMutations,
                    session,
                    actor,
                    dispatcher),
                [NavigationProjector.IngredientsWorkspace] = IngredientsViewModel.CreateFactory(
                    ingredients,
                    services.GetRequiredService<IngredientActionProjector>(),
                    taggedMutations,
                    session,
                    actor,
                    dispatcher),
                [NavigationProjector.InventoryWorkspace] = InventoryViewModel.CreateFactory(
                    inventory,
                    ingredients,
                    services.GetRequiredService<InventoryActionProjector>(),
                    taggedMutations,
                    session,
                    actor,
                    dispatcher),
                [NavigationProjector.MenusWorkspace] = MenusViewModel.CreateFactory(
                    menus,
                    drinks,
                    services.GetRequiredService<MenuActionProjector>(),
                    taggedMutations,
                    session,
                    actor,
                    dispatcher),
                [NavigationProjector.OrdersWorkspace] = OrdersViewModel.CreateFactory(
                    orders,
                    menus,
                    drinks,
                    services.GetRequiredService<OrderActionProjector>(),
                    taggedMutations,
                    session,
                    actor,
                    dispatcher),
                [NavigationProjector.AuditWorkspace] = AuditViewModel.CreateFactory(
                    audit,
                    services.GetRequiredService<AuditActionProjector>(),
                    session,
                    actor,
                    dispatcher),
                [NavigationProjector.TagsWorkspace] = TagsViewModel.CreateFactory(
                    tagging,
                    services.GetRequiredService<TaggingActionProjector>(),
                    drinks,
                    ingredients,
                    inventory,
                    menus,
                    orders,
                    session,
                    actor,
                    dispatcher),
            };

        SqliteChangeMonitor changes = services.GetRequiredService<MixologyStore>().MonitorChanges();
        ShellViewModel? shell = null;
        try
        {
            await changes.Ready.WaitAsync(cancellationToken).ConfigureAwait(false);
            shell = new(
                navigation,
                workspaces,
                confirmation,
                dispatcher,
                changes,
                ownsMonitor: true,
                actor: actor.ToString());
            await shell.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return shell;
        }
        catch
        {
            if (shell is null)
            {
                await changes.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await shell.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }
}
