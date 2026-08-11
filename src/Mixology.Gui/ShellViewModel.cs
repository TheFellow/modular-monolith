using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mixology.Gui.Navigation;
using Mixology.Gui.Workspaces;
using Mixology.Gui.Workspaces.Dashboard;
using Mixology.Kernel.Errors;
using Mixology.Persistence;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Gui;

public sealed partial class ShellViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IReadOnlyDictionary<WorkspaceId, Func<IDesktopWorkspace>> factories;
    private readonly IDirtyNavigationConfirmation confirmation;
    private readonly IUiDispatcher dispatcher;
    private readonly object sync = new();
    private readonly Dictionary<WorkspaceId, IDesktopWorkspace> cache = [];
    private readonly HashSet<WorkspaceId> activated = [];
    private readonly HashSet<WorkspaceId> stale = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly IAsyncDisposable? ownedMonitor;
    private readonly bool ownsMonitor;
    private readonly Task? changes;
    private int refreshingStale;
    private bool disposed;

    public ShellViewModel(
        NavigationProjection projection,
        IReadOnlyDictionary<WorkspaceId, Func<IDesktopWorkspace>> factories,
        IDirtyNavigationConfirmation? confirmation = null,
        IUiDispatcher? dispatcher = null,
        IStoreChangeSource? monitor = null,
        bool ownsMonitor = false,
        string actor = "owner")
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(factories);
        this.factories = factories;
        this.confirmation = confirmation ?? new RejectDirtyNavigationConfirmation();
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        ownedMonitor = ownsMonitor ? monitor as IAsyncDisposable : null;
        this.ownsMonitor = ownsMonitor;
        Navigation = projection.Items
            .Where(item => factories.ContainsKey(item.Id))
            .Select(item => new DesktopNavigationItemViewModel(item, NavigateAsync))
            .ToArray();
        changes = monitor is null ? null : ObserveChangesAsync(monitor, lifetime.Token);
        Identity = $"Local user · {actor}";
    }

    public IReadOnlyList<DesktopNavigationItemViewModel> Navigation { get; }

    public string Identity { get; }

    [ObservableProperty]
    public partial IDesktopWorkspace? ActiveWorkspace { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public Exception? LastError { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        DesktopNavigationItemViewModel dashboard = Navigation.Single(item =>
            item.Id == NavigationProjector.DashboardWorkspace);
        await NavigateAsync(dashboard, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> NavigateAsync(
        DesktopNavigationItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!factories.ContainsKey(item.Id))
        {
            return false;
        }

        if (ActiveWorkspace?.Id == item.Id)
        {
            return true;
        }

        if (ActiveWorkspace is { IsDirty: true } dirty &&
            !await confirmation.ConfirmDiscardAsync(dirty, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            IDesktopWorkspace workspace;
            lock (sync)
            {
                if (!cache.TryGetValue(item.Id, out workspace!))
                {
                    workspace = factories[item.Id]();
                    if (workspace is DashboardViewModel dashboard)
                    {
                        dashboard.SetNavigation(NavigateToAsync);
                    }

                    cache.Add(item.Id, workspace);
                    workspace.PropertyChanged += OnWorkspacePropertyChanged;
                }
            }

            bool activate;
            lock (sync)
            {
                activate = !activated.Contains(item.Id) || stale.Remove(item.Id);
            }

            if (activate)
            {
                await workspace.ActivateAsync(cancellationToken).ConfigureAwait(false);
                lock (sync)
                {
                    _ = activated.Add(item.Id);
                }
            }

            await dispatcher.InvokeAsync(() =>
            {
                ActiveWorkspace = workspace;
                foreach (DesktopNavigationItemViewModel navigationItem in Navigation)
                {
                    navigationItem.IsActive = navigationItem.Id == item.Id;
                }

                LastError = null;
                StatusMessage = string.Empty;
            }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            Exception error = AppError.Find(exception)
                ?? AppError.Internal($"open desktop workspace {item.Id}", exception);
            await dispatcher.InvokeAsync(() =>
            {
                LastError = error;
                StatusMessage = AppError.Find(error)?.UserMessage ?? "internal error";
            }, cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    private Task NavigateToAsync(WorkspaceId id, CancellationToken cancellationToken)
    {
        DesktopNavigationItemViewModel item = Navigation.Single(candidate => candidate.Id == id);
        return NavigateAsync(item, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        if (changes is not null)
        {
            try
            {
                await changes.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (ownsMonitor && ownedMonitor is not null)
        {
            await ownedMonitor.DisposeAsync().ConfigureAwait(false);
        }

        Exception? failure = null;
        foreach (IDesktopWorkspace workspace in cache.Values.Reverse())
        {
            workspace.PropertyChanged -= OnWorkspacePropertyChanged;
            try
            {
                await workspace.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        cache.Clear();
        activated.Clear();
        stale.Clear();
        lifetime.Dispose();
        if (failure is not null)
        {
            Exception normalized = AppError.IsCancellation(failure)
                ? failure
                : AppError.Find(failure) ?? AppError.Internal("close desktop shell", failure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(normalized).Throw();
        }
    }

    private async Task ObserveChangesAsync(IStoreChangeSource source, CancellationToken cancellationToken)
    {
        await foreach (long epoch in source.Changes.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = epoch;
            lock (sync)
            {
                foreach (WorkspaceId id in cache.Keys)
                {
                    _ = stale.Add(id);
                }
            }

            IDesktopWorkspace? workspace = ActiveWorkspace;
            if (workspace is null || workspace.IsDirty)
            {
                continue;
            }

            await RefreshStaleAsync(workspace, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(IDesktopWorkspace.IsDirty) &&
            sender is IDesktopWorkspace workspace &&
            workspace == ActiveWorkspace &&
            !workspace.IsDirty &&
            IsStale(workspace.Id))
        {
            _ = RefreshStaleAsync(workspace, lifetime.Token);
        }
    }

    private async Task RefreshStaleAsync(IDesktopWorkspace workspace, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref refreshingStale, 1) != 0)
        {
            return;
        }

        try
        {
            while (workspace == ActiveWorkspace && !workspace.IsDirty && TakeStale(workspace.Id))
            {
                try
                {
                    await workspace.ActivateAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception))
                {
                }
                catch (Exception exception)
                {
                    Exception error = AppError.Find(exception)
                        ?? AppError.Internal($"refresh desktop workspace {workspace.Id}", exception);
                    await dispatcher.InvokeAsync(() =>
                    {
                        LastError = error;
                        StatusMessage = AppError.Find(error)?.UserMessage ?? "internal error";
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref refreshingStale, 0);
        }
    }

    private bool IsStale(WorkspaceId id)
    {
        lock (sync)
        {
            return stale.Contains(id);
        }
    }

    private bool TakeStale(WorkspaceId id)
    {
        lock (sync)
        {
            return stale.Remove(id);
        }
    }
}
