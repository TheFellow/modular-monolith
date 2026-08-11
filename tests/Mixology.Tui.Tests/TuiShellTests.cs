using System.Threading.Channels;
using Mixology.Kernel.Errors;
using Mixology.Persistence;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class TuiShellTests
{
    [Fact]
    public async Task DatabaseInvalidationRefreshesBrowseAndDefersOwnedInput()
    {
        FakeChangeSource changes = new();
        FakeWorkspace dashboard = new(TuiRoutes.Dashboard.Id);
        await using TuiShell shell = new(
            Navigation(TuiRoutes.Dashboard),
            new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
            {
                [dashboard.Id] = () => dashboard,
            },
            changes);
        await shell.StartAsync();

        changes.Publish();
        await WaitUntilAsync(() => dashboard.RefreshCalls == 1);
        dashboard.SetOwnership(InputOwnership.Edit);
        changes.Publish();
        await Task.Delay(50);
        Assert.Equal(1, dashboard.RefreshCalls);

        dashboard.SetOwnership(InputOwnership.Browse);
        await WaitUntilAsync(() => dashboard.RefreshCalls == 2);
    }

    [Fact]
    public void CanonicalRoutesKeepReferenceOrderAndOnlyOneThroughSevenShortcuts()
    {
        Assert.Equal(
            ["dashboard", "drinks", "ingredients", "inventory", "menus", "orders", "audit", "tags"],
            TuiRoutes.All.Select(static route => route.Id.Value));
        Assert.Null(TuiRoutes.Dashboard.Shortcut);
        Assert.Equal(['1', '2', '3', '4', '5', '6', '7'],
            TuiRoutes.All.Skip(1).Select(static route => route.Shortcut));
    }

    [Fact]
    public async Task OnlyAuthorizedImplementedRoutesAreRegistered()
    {
        NavigationProjection navigation = new(
        [
            new(TuiRoutes.Dashboard.Id, "Dashboard"),
            new(TuiRoutes.Drinks.Id, "Drinks"),
            new(TuiRoutes.Audit.Id, "Audit"),
        ],
        []);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new FakeWorkspace(TuiRoutes.Dashboard.Id),
            [TuiRoutes.Tags.Id] = () => new FakeWorkspace(TuiRoutes.Tags.Id),
        });

        Assert.Equal([TuiRoutes.Dashboard.Id], shell.Routes.Select(static route => route.Id));
        Assert.False(await shell.NavigateAsync(TuiRoutes.Drinks.Id));
        Assert.False(await shell.NavigateAsync(TuiRoutes.Tags.Id));
    }

    [Fact]
    public async Task BackRecreatesDashboardWhileOtherWorkspacesAreCached()
    {
        int dashboards = 0;
        int drinks = 0;
        NavigationProjection navigation = Navigation(TuiRoutes.Dashboard, TuiRoutes.Drinks);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new FakeWorkspace(TuiRoutes.Dashboard.Id, ++dashboards),
            [TuiRoutes.Drinks.Id] = () => new FakeWorkspace(TuiRoutes.Drinks.Id, ++drinks),
        });

        await shell.StartAsync();
        Assert.True(await shell.NavigateAsync(TuiRoutes.Drinks.Id));
        Assert.True(await shell.BackAsync());
        Assert.True(await shell.NavigateAsync(TuiRoutes.Drinks.Id));

        Assert.Equal(2, dashboards);
        Assert.Equal(1, drinks);
        Assert.Equal(TuiRoutes.Drinks.Id, shell.CurrentRoute.Id);
    }

    [Fact]
    public async Task HelpAndLocalInputOwnBackBeforeNavigation()
    {
        FakeWorkspace? drinks = null;
        NavigationProjection navigation = Navigation(TuiRoutes.Dashboard, TuiRoutes.Drinks);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new FakeWorkspace(TuiRoutes.Dashboard.Id),
            [TuiRoutes.Drinks.Id] = () => drinks = new FakeWorkspace(
                TuiRoutes.Drinks.Id,
                ownership: InputOwnership.Edit,
                handlesKeys: false),
        });
        await shell.StartAsync();
        _ = await shell.NavigateAsync(TuiRoutes.Drinks.Id);

        drinks!.Ownership = InputOwnership.Browse;
        drinks.HandledKey = '?';
        _ = await shell.HandleAsync('?');
        Assert.False(shell.ShowHelp);
        drinks.HandledKey = null;
        _ = await shell.HandleAsync('?');
        Assert.True(shell.ShowHelp);
        _ = await shell.BackAsync();
        Assert.False(shell.ShowHelp);
        Assert.Equal(TuiRoutes.Drinks.Id, shell.CurrentRoute.Id);

        drinks.Ownership = InputOwnership.Edit;
        _ = await shell.BackAsync();
        Assert.Equal(1, drinks!.Handled.Count(static key => key == '\u001b'));
        Assert.Equal(TuiRoutes.Drinks.Id, shell.CurrentRoute.Id);
        drinks.Ownership = InputOwnership.Browse;
        _ = await shell.BackAsync();
        Assert.Equal(TuiRoutes.Dashboard.Id, shell.CurrentRoute.Id);
    }

    [Fact]
    public async Task MinimumViewportIsAppliedOnceAtTheShellBoundary()
    {
        NavigationProjection navigation = Navigation(TuiRoutes.Dashboard);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new FakeWorkspace(TuiRoutes.Dashboard.Id),
        });
        await shell.StartAsync();

        Assert.Contains("content 80x21", shell.Render(new Viewport(80, 24)), StringComparison.Ordinal);
        Assert.Contains("Terminal too small", shell.Render(new Viewport(79, 24)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NavigationProjectionErrorsUseSafeStatusMapping()
    {
        NavigationProjection navigation = new(
            [new(TuiRoutes.Dashboard.Id, "Dashboard")],
            [new IOException("secret database path")]);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new FakeWorkspace(TuiRoutes.Dashboard.Id),
        });
        await shell.StartAsync();

        Assert.Equal("internal error", shell.Status?.Message);
        Assert.Equal(TerminalErrorStyle.Error, shell.Status?.Style);
        Assert.Contains("internal error", shell.Render(new Viewport(80, 24)), StringComparison.Ordinal);
    }

    private static NavigationProjection Navigation(params TuiRoute[] routes) => new(
        routes.Select(static route => new NavigationItem(route.Id, route.Label)).ToArray(),
        []);

    private sealed class FakeWorkspace : ITuiWorkspace
    {
        private readonly int instance;

        public FakeWorkspace(
            WorkspaceId id,
            int instance = 0,
            InputOwnership ownership = default,
            bool handlesKeys = true)
        {
            Id = id;
            this.instance = instance;
            Ownership = ownership;
            HandlesKeys = handlesKeys;
        }

        public WorkspaceId Id { get; }
        public string Title => Id.Value;
        public InputOwnership Ownership { get; set; }
        public bool HandlesKeys { get; }
        public char? HandledKey { get; set; }
        public InputOwnership InputOwnership => Ownership;
        public TuiError? Status => null;
        public List<char> Handled { get; } = [];
        public int RefreshCalls { get; private set; }
        public event Action? Changed;
        public Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCalls++;
            return Task.CompletedTask;
        }
        public string Render(Viewport viewport) => $"content {viewport.Width}x{viewport.Height} instance {instance}";

        public bool Handle(char key)
        {
            Handled.Add(key);
            Changed?.Invoke();
            return key == HandledKey || HandlesKeys && (Ownership.CapturesText ||
                Ownership.HandlesBack && key == '\u001b');
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void SetOwnership(InputOwnership ownership)
        {
            Ownership = ownership;
            Changed?.Invoke();
        }
    }

    private sealed class FakeChangeSource : IStoreChangeSource
    {
        private readonly Channel<long> changes = Channel.CreateUnbounded<long>();
        private long epoch;

        public ChannelReader<long> Changes => changes.Reader;

        public void Publish() => _ = changes.Writer.TryWrite(Interlocked.Increment(ref epoch));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
