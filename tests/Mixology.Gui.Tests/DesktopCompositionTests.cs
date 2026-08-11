using Mixology.Application.Authentication;
using Mixology.Gui.Navigation;
using Xunit;

namespace Mixology.Gui.Tests;

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
