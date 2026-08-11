using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using Mixology.Desktop.Navigation;
using Mixology.Desktop.Workspaces;
using Mixology.Kernel.Errors;
using Mixology.Persistence;
using Mixology.Presentation.Navigation;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task DatabaseInvalidationRefreshesCleanWorkspaceAndDefersDirtyWorkspace()
    {
        FakeChangeSource changes = new();
        FakeWorkspace dashboard = new(NavigationProjector.DashboardWorkspace, "Dashboard");
        await using ShellViewModel shell = new(
            Projection(NavigationProjector.DrinksWorkspace),
            new Dictionary<WorkspaceId, Func<IDesktopWorkspace>>
            {
                [dashboard.Id] = () => dashboard,
                [NavigationProjector.DrinksWorkspace] = () =>
                    new FakeWorkspace(NavigationProjector.DrinksWorkspace, "Drinks"),
            },
            new Confirmation(true),
            monitor: changes);
        await shell.InitializeAsync();

        changes.Publish();
        await WaitUntilAsync(() => dashboard.ActivationCalls == 2);
        dashboard.SetDirty(true);
        changes.Publish();
        await Task.Delay(50);
        Assert.Equal(2, dashboard.ActivationCalls);

        dashboard.SetDirty(false);
        await WaitUntilAsync(() => dashboard.ActivationCalls == 3);
    }

    [Fact]
    public async Task OnlyAuthorizedImplementedRoutesAreAdvertisedAndWorkspacesAreLazyCached()
    {
        WorkspaceId other = NavigationProjector.DrinksWorkspace;
        NavigationProjection projection = new(
            [
                new NavigationItem(NavigationProjector.DashboardWorkspace, "Dashboard"),
                new NavigationItem(other, "Drinks"),
                new NavigationItem(NavigationProjector.InventoryWorkspace, "Inventory"),
            ],
            []);
        int dashboards = 0;
        int drinks = 0;
        Dictionary<WorkspaceId, Func<IDesktopWorkspace>> factories = new()
        {
            [NavigationProjector.DashboardWorkspace] = () =>
            {
                dashboards++;
                return new FakeWorkspace(NavigationProjector.DashboardWorkspace, "Dashboard");
            },
            [other] = () =>
            {
                drinks++;
                return new FakeWorkspace(other, "Drinks");
            },
        };
        await using ShellViewModel shell = new(projection, factories, new Confirmation(true));

        Assert.Equal(["Dashboard", "Drinks"], shell.Navigation.Select(item => item.Label));
        Assert.Equal(0, dashboards);
        await shell.InitializeAsync();
        Assert.Equal(1, dashboards);
        DesktopNavigationItemViewModel drinksItem = shell.Navigation.Single(item => item.Id == other);
        Assert.True(await shell.NavigateAsync(drinksItem));
        Assert.Equal(1, drinks);
        DesktopNavigationItemViewModel dashboard = shell.Navigation.Single(item =>
            item.Id == NavigationProjector.DashboardWorkspace);
        Assert.True(await shell.NavigateAsync(dashboard));
        Assert.True(await shell.NavigateAsync(drinksItem));
        Assert.Equal(1, drinks);
    }

    [Fact]
    public async Task DirtyWorkspaceMustBeConfirmedBeforeNavigation()
    {
        WorkspaceId other = NavigationProjector.DrinksWorkspace;
        FakeWorkspace dashboard = new(NavigationProjector.DashboardWorkspace, "Dashboard") { IsDirtyValue = true };
        Confirmation confirmation = new(false);
        await using ShellViewModel shell = new(
            Projection(other),
            new Dictionary<WorkspaceId, Func<IDesktopWorkspace>>
            {
                [dashboard.Id] = () => dashboard,
                [other] = () => new FakeWorkspace(other, "Drinks"),
            },
            confirmation);
        await shell.InitializeAsync();

        bool changed = await shell.NavigateAsync(shell.Navigation.Single(item => item.Id == other));

        Assert.False(changed);
        Assert.Same(dashboard, shell.ActiveWorkspace);
        Assert.Equal(1, confirmation.Calls);
    }

    [Fact]
    public async Task WorkspaceFailuresPreserveTypedErrorsAndNormalizeUnknownCauses()
    {
        WorkspaceId other = NavigationProjector.DrinksWorkspace;
        InvalidError typed = AppError.Invalid("invalid workspace");
        await using ShellViewModel typedShell = ShellWithFailure(other, typed);
        await typedShell.InitializeAsync();
        Assert.False(await typedShell.NavigateAsync(typedShell.Navigation.Single(item => item.Id == other)));
        Assert.Same(typed, typedShell.LastError);

        InvalidOperationException cause = new("implementation detail");
        await using ShellViewModel unknownShell = ShellWithFailure(other, cause);
        await unknownShell.InitializeAsync();
        Assert.False(await unknownShell.NavigateAsync(unknownShell.Navigation.Single(item => item.Id == other)));
        InternalError error = Assert.IsType<InternalError>(unknownShell.LastError);
        Assert.Same(cause, error.InnerException);
        Assert.Equal("internal error", unknownShell.StatusMessage);
    }

    [Fact]
    public async Task FailedWorkspaceActivationCanBeRetried()
    {
        WorkspaceId other = NavigationProjector.DrinksWorkspace;
        FakeWorkspace retry = new(other, "Drinks")
        {
            ActivationError = AppError.Conflict("temporarily unavailable"),
        };
        await using ShellViewModel shell = new(
            Projection(other),
            new Dictionary<WorkspaceId, Func<IDesktopWorkspace>>
            {
                [NavigationProjector.DashboardWorkspace] = () =>
                    new FakeWorkspace(NavigationProjector.DashboardWorkspace, "Dashboard"),
                [other] = () => retry,
            },
            new Confirmation(true));
        await shell.InitializeAsync();
        DesktopNavigationItemViewModel item = shell.Navigation.Single(value => value.Id == other);

        Assert.False(await shell.NavigateAsync(item));
        retry.ActivationError = null;
        Assert.True(await shell.NavigateAsync(item));

        Assert.Equal(2, retry.ActivationCalls);
        Assert.Same(retry, shell.ActiveWorkspace);
    }

    private static ShellViewModel ShellWithFailure(WorkspaceId other, Exception error) => new(
        Projection(other),
        new Dictionary<WorkspaceId, Func<IDesktopWorkspace>>
        {
            [NavigationProjector.DashboardWorkspace] = () =>
                new FakeWorkspace(NavigationProjector.DashboardWorkspace, "Dashboard"),
            [other] = () => new FakeWorkspace(other, "Drinks") { ActivationError = error },
        },
        new Confirmation(true));

    private static NavigationProjection Projection(WorkspaceId other) => new(
        [
            new NavigationItem(NavigationProjector.DashboardWorkspace, "Dashboard"),
            new NavigationItem(other, "Drinks"),
        ],
        []);

    private sealed class FakeWorkspace(WorkspaceId id, string title) : ObservableObject, IDesktopWorkspace
    {
        public WorkspaceId Id { get; } = id;
        public string Title { get; } = title;
        public bool IsDirty => IsDirtyValue;
        public bool IsDirtyValue { get; set; }
        public Exception? ActivationError { get; set; }
        public int ActivationCalls { get; private set; }

        public Task ActivateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActivationCalls++;
            return ActivationError is null ? Task.CompletedTask : Task.FromException(ActivationError);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void SetDirty(bool value)
        {
            IsDirtyValue = value;
            OnPropertyChanged(nameof(IsDirty));
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

    private sealed class Confirmation(bool answer) : IDirtyNavigationConfirmation
    {
        public int Calls { get; private set; }

        public Task<bool> ConfirmDiscardAsync(
            IDesktopWorkspace workspace,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(answer);
        }
    }
}
