using CommunityToolkit.Mvvm.ComponentModel;
using Mixology.Desktop.Navigation;
using Mixology.Desktop.Workspaces;
using Mixology.Kernel.Errors;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Desktop;

public sealed partial class ShellViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IReadOnlyDictionary<WorkspaceId, Func<IDesktopWorkspace>> factories;
    private readonly IDirtyNavigationConfirmation confirmation;
    private readonly IUiDispatcher dispatcher;
    private readonly Dictionary<WorkspaceId, IDesktopWorkspace> cache = [];
    private readonly HashSet<WorkspaceId> activated = [];
    private bool disposed;

    public ShellViewModel(
        NavigationProjection projection,
        IReadOnlyDictionary<WorkspaceId, Func<IDesktopWorkspace>> factories,
        IDirtyNavigationConfirmation? confirmation = null,
        IUiDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(factories);
        this.factories = factories;
        this.confirmation = confirmation ?? new RejectDirtyNavigationConfirmation();
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        Navigation = projection.Items
            .Where(item => factories.ContainsKey(item.Id))
            .Select(item => new DesktopNavigationItemViewModel(item, NavigateAsync))
            .ToArray();
    }

    public IReadOnlyList<DesktopNavigationItemViewModel> Navigation { get; }

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
            if (!cache.TryGetValue(item.Id, out IDesktopWorkspace? workspace))
            {
                workspace = factories[item.Id]();
                cache.Add(item.Id, workspace);
            }

            if (!activated.Contains(item.Id))
            {
                await workspace.ActivateAsync(cancellationToken).ConfigureAwait(false);
                _ = activated.Add(item.Id);
            }

            await dispatcher.InvokeAsync(() =>
            {
                ActiveWorkspace = workspace;
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

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Exception? failure = null;
        foreach (IDesktopWorkspace workspace in cache.Values.Reverse())
        {
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
        if (failure is not null)
        {
            Exception normalized = AppError.IsCancellation(failure)
                ? failure
                : AppError.Find(failure) ?? AppError.Internal("close desktop shell", failure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(normalized).Throw();
        }
    }
}
