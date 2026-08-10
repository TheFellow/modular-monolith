using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mixology.Kernel.Errors;
using Mixology.Presentation.Dashboard;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Desktop.Workspaces.Dashboard;

public sealed record DashboardActivityViewModel(string Timestamp, string Actor, string Action);

public sealed partial class DashboardViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly Func<CancellationToken, Task<DashboardResult>> load;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<DashboardLoadOutcome> requests = new();
    private bool disposed;

    public DashboardViewModel(
        Func<CancellationToken, Task<DashboardResult>> load,
        IUiDispatcher? dispatcher = null)
    {
        this.load = load ?? throw new ArgumentNullException(nameof(load));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = new AsyncRelayCommand(
            RefreshAsync,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public WorkspaceId Id => NavigationProjector.DashboardWorkspace;

    public string Title => "Dashboard";

    public bool IsDirty => false;

    public IAsyncRelayCommand RefreshCommand { get; }

    public ObservableCollection<DashboardActivityViewModel> RecentActivity { get; } = [];

    [ObservableProperty]
    public partial string DrinkCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string IngredientCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string InventoryCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string LowStockCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string MenuCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string MenuStatus { get; set; } = Unknown;

    [ObservableProperty]
    public partial string OrderCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string PendingOrderCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial string AuditCount { get; set; } = Unknown;

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public Exception? Error { get; private set; }

    private const string Unknown = "—";

    public Task ActivateAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() => IsRefreshing = true, cancellationToken).ConfigureAwait(false);
        try
        {
            LatestResult<DashboardLoadOutcome> latest = await requests.RunAsync(
                LoadAsync,
                cancellationToken).ConfigureAwait(false);
            if (!latest.IsCurrent || latest.Value is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() => Publish(latest.Value), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested)
        {
            // A newer request owns publication and the refreshing state.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await requests.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<DashboardLoadOutcome> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            DashboardResult result = await load(cancellationToken).ConfigureAwait(false);
            if (AppError.IsCancellation(result.Error))
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Error!).Throw();
            }

            Exception? error = result.Error is null
                ? null
                : AppError.Find(result.Error)
                    ?? AppError.Internal("load desktop dashboard", result.Error);
            return new DashboardLoadOutcome(result.Data, error);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DashboardLoadOutcome(
                DashboardData.Unknown,
                AppError.Find(exception) ?? AppError.Internal("load desktop dashboard", exception));
        }
    }

    private void Publish(DashboardLoadOutcome outcome)
    {
        DashboardData data = outcome.Data;
        DrinkCount = Count(data.DrinkCount);
        IngredientCount = Count(data.IngredientCount);
        InventoryCount = Count(data.InventoryCount);
        LowStockCount = Count(data.LowStockCount);
        MenuCount = Count(data.MenuCount);
        MenuStatus = data.DraftMenus < 0 || data.PublishedMenus < 0
            ? Unknown
            : $"{data.DraftMenus} draft / {data.PublishedMenus} published";
        OrderCount = Count(data.OrderCount);
        PendingOrderCount = Count(data.PendingOrders);
        AuditCount = Count(data.AuditCount);
        RecentActivity.Clear();
        foreach (DashboardActivity activity in data.RecentActivity)
        {
            RecentActivity.Add(new DashboardActivityViewModel(
                activity.Timestamp.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture),
                activity.Actor,
                activity.Action));
        }

        Error = outcome.Error;
        StatusMessage = AppError.Find(outcome.Error)?.UserMessage ?? string.Empty;
        IsRefreshing = false;
    }

    private static string Count(int value) => value < 0
        ? Unknown
        : value.ToString(System.Globalization.CultureInfo.CurrentCulture);

    private sealed record DashboardLoadOutcome(DashboardData Data, Exception? Error);
}
