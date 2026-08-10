using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mixology.Application.Authentication;
using Mixology.Desktop.Navigation;
using Mixology.Desktop.Workspaces.Audit;
using Mixology.Desktop.Workspaces.Dashboard;
using Mixology.Desktop.Workspaces.Drinks;
using Mixology.Desktop.Workspaces.Ingredients;
using Mixology.Desktop.Workspaces.Inventory;
using Mixology.Desktop.Workspaces.Menus;
using Mixology.Desktop.Workspaces.Orders;
using Mixology.Desktop.Workspaces.Tags;
using Mixology.Presentation.Navigation;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class DesktopCompositionTests
{
    public static TheoryData<string, string[]> AuthorizedRouteCases => new()
    {
        {
            "owner",
            ["dashboard", "drinks", "ingredients", "inventory", "menus", "orders", "audit", "tags"]
        },
        {
            "manager",
            ["dashboard", "drinks", "ingredients", "inventory", "menus", "orders"]
        },
        {
            "anonymous",
            ["dashboard", "drinks", "ingredients", "inventory", "menus"]
        },
    };

    [Theory]
    [MemberData(nameof(AuthorizedRouteCases))]
    public async Task ProductionShellAdvertisesOnlyAuthorizedMountableWorkspaces(
        string actor,
        string[] expectedRoutes)
    {
        await using TemporaryDesktopHost temporary = await TemporaryDesktopHost.OpenAsync(actor);
        await using ShellViewModel shell = await DesktopShellFactory.CreateAsync(
            temporary.Host.Services,
            Actor.Parse(actor),
            new RejectDirtyNavigationConfirmation(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expectedRoutes, shell.Navigation.Select(static item => item.Id.Value));
        foreach (DesktopNavigationItemViewModel item in shell.Navigation)
        {
            Assert.True(await shell.NavigateAsync(item, TestContext.Current.CancellationToken));
            Assert.Equal(item.Id, shell.ActiveWorkspace?.Id);
        }
    }

    [AvaloniaFact]
    public async Task MainWindowResolvesEveryOwnerWorkspaceToItsBespokeView()
    {
        await using TemporaryDesktopHost temporary = await TemporaryDesktopHost.OpenAsync("owner");
        await using ShellViewModel shell = await DesktopShellFactory.CreateAsync(
            temporary.Host.Services,
            Actor.Owner,
            new RejectDirtyNavigationConfirmation(),
            cancellationToken: TestContext.Current.CancellationToken);
        MainWindow window = new(shell);
        window.Show();
        try
        {
            Dictionary<WorkspaceId, Type> expected = new()
            {
                [NavigationProjector.DashboardWorkspace] = typeof(DashboardView),
                [NavigationProjector.DrinksWorkspace] = typeof(DrinksWorkspaceView),
                [NavigationProjector.IngredientsWorkspace] = typeof(IngredientsView),
                [NavigationProjector.InventoryWorkspace] = typeof(InventoryView),
                [NavigationProjector.MenusWorkspace] = typeof(MenusView),
                [NavigationProjector.OrdersWorkspace] = typeof(OrdersView),
                [NavigationProjector.AuditWorkspace] = typeof(AuditView),
                [NavigationProjector.TagsWorkspace] = typeof(TagsView),
            };

            foreach (DesktopNavigationItemViewModel item in shell.Navigation)
            {
                Assert.True(await shell.NavigateAsync(item, TestContext.Current.CancellationToken));
                Assert.Contains(window.GetVisualDescendants(), control =>
                    control.GetType() == expected[item.Id]);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TemporaryDesktopHost(string root, DesktopHost host) : IAsyncDisposable
    {
        public DesktopHost Host { get; } = host;

        public static async Task<TemporaryDesktopHost> OpenAsync(string actor)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mixology-desktop-composition",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DesktopHost host = await DesktopHost.OpenAsync(
                    DesktopOptions.Create(
                        Path.Combine(root, "mixology.db"),
                        actor,
                        "error",
                        "text",
                        Path.Combine(root, "desktop.log"),
                        metrics: false),
                    TestContext.Current.CancellationToken);
                return new TemporaryDesktopHost(root, host);
            }
            catch
            {
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
