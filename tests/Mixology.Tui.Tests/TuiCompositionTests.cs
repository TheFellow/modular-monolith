using Microsoft.Data.Sqlite;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Serilog.Events;
using Terminal.Gui.Input;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class TuiCompositionTests
{
    [Fact]
    public async Task HostedRuntimeMigratesRealStoreAndAdvertisesOnlyImplementedRoutes()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-tui-host", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        string log = Path.Combine(root, "mixology-tui.log");
        CapturingRunner runner = new();
        TuiOptions options = TuiOptions.Create(database, "owner", "error", "text", log, metrics: false);

        try
        {
            await new HostedTuiRuntime(runner).RunAsync(options);

            Assert.True(File.Exists(database));
            Assert.Equal(
                [TuiRoutes.Dashboard.Id, TuiRoutes.Ingredients.Id, TuiRoutes.Inventory.Id],
                runner.Routes);
            Assert.Contains("Mixology > Dashboard", runner.Screen, StringComparison.Ordinal);
            Assert.Contains("Drinks", runner.Screen, StringComparison.Ordinal);
            Assert.Contains("Recent Activity", runner.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("[1]", runner.Screen, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TerminalKeyAdapterPreservesControlSubmissionAndQuit()
    {
        Assert.Equal('\u0013', MixologyWindow.MapInput(new Key('s').WithCtrl));
        Assert.Equal('q', MixologyWindow.MapInput(new Key('c').WithCtrl));
        Assert.Equal('s', MixologyWindow.MapInput(new Key('s')));
    }

    [Fact]
    public async Task AnsiApplicationSeamConstructsProductionWindowWithoutStaticApplicationState()
    {
        using TerminalApplicationHost host = TerminalApplicationHost.CreateAnsi();
        host.Initialize();
        NavigationProjection navigation = new([new(TuiRoutes.Dashboard.Id, "Dashboard")], []);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new StaticWorkspace(),
        });
        await shell.StartAsync();
        using MixologyWindow window = new(host.Application, shell);

        string rendered = window.Render(new Viewport(80, 24));

        Assert.Contains("Mixology > Dashboard", rendered, StringComparison.Ordinal);
        Assert.NotNull(host.Application.Driver);
        Assert.Equal(TerminalApplicationState.Initialized, host.State);
    }

    [Fact]
    public async Task PreCancelledTerminalRunPreservesCancellationAndDisposesAnsiHost()
    {
        int hosts = 0;
        TerminalGuiRunner runner = new(() =>
        {
            hosts++;
            return TerminalApplicationHost.CreateAnsi();
        });
        NavigationProjection navigation = new([new(TuiRoutes.Dashboard.Id, "Dashboard")], []);
        await using TuiShell shell = new(navigation, new Dictionary<WorkspaceId, Func<ITuiWorkspace>>
        {
            [TuiRoutes.Dashboard.Id] = () => new StaticWorkspace(),
        });
        await shell.StartAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runner.RunAsync(shell, cancellation.Token));
        Assert.Equal(1, hosts);
    }

    private sealed class CapturingRunner : ITuiRunner
    {
        public string Screen { get; private set; } = string.Empty;
        public WorkspaceId[] Routes { get; private set; } = [];

        public Task RunAsync(TuiShell shell, CancellationToken cancellationToken = default)
        {
            Screen = shell.Render(new Viewport(100, 40));
            Routes = shell.Routes.Select(static route => route.Id).ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class StaticWorkspace : ITuiWorkspace
    {
        public WorkspaceId Id => TuiRoutes.Dashboard.Id;
        public string Title => "Dashboard";
        public InputOwnership InputOwnership => InputOwnership.Browse;
        public TuiError? Status => null;
        public event Action? Changed
        {
            add { }
            remove { }
        }
        public Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string Render(Viewport viewport) => "Dashboard content";
        public bool Handle(char key) => false;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
