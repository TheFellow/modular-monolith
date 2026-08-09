using System.Globalization;
using System.Text;
using Mixology.Kernel.Errors;
using Mixology.Presentation.Dashboard;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Tui.Workspaces;

public sealed class DashboardWorkspace(
    Func<CancellationToken, Task<DashboardResult>> load) : ITuiWorkspace
{
    private readonly object sync = new();
    private readonly List<Task> requests = [];
    private readonly List<CancellationTokenSource> requestSources = [];
    private CancellationTokenSource? requestCancellation;
    private Task current = Task.CompletedTask;
    private long generation;
    private DashboardData data = DashboardData.Unknown;
    private Exception? error;
    private bool loading;
    private bool disposed;

    public WorkspaceId Id => NavigationProjector.DashboardWorkspace;
    public string Title => "Dashboard";
    public InputOwnership InputOwnership => InputOwnership.Browse;
    public TuiError? Status
    {
        get
        {
            lock (sync)
            {
                return error is null ? null : TuiErrorAdapter.Adapt(error);
            }
        }
    }
    public event Action? Changed;

    public Task ActivateAsync(CancellationToken cancellationToken = default) => RefreshAsync(cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource next = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource? previous;
        long request;
        lock (sync)
        {
            if (disposed)
            {
                next.Dispose();
                throw new ObjectDisposedException(nameof(DashboardWorkspace));
            }

            previous = requestCancellation;
            requestCancellation = next;
            request = ++generation;
            loading = true;
            error = null;
            current = LoadAsync(request, next);
            requests.Add(current);
            requestSources.Add(next);
        }

        previous?.Cancel();
        Changed?.Invoke();
        return current;
    }

    public bool Handle(char key)
    {
        _ = key;
        return false;
    }

    public string Render(Viewport viewport)
    {
        DashboardData snapshot;
        bool isLoading;
        lock (sync)
        {
            snapshot = data;
            isLoading = loading;
        }

        StringBuilder rendered = new();
        _ = rendered.AppendLine("Dashboard");
        _ = rendered.AppendLine("Select a workspace to continue");
        _ = rendered.AppendLine();
        AppendCard(rendered, TuiRoutes.Drinks, snapshot.DrinkCount, "Manage drink recipes");
        AppendCard(rendered, TuiRoutes.Ingredients, snapshot.IngredientCount, "Catalog ingredients");
        AppendCard(rendered, TuiRoutes.Inventory, snapshot.InventoryCount, LowStock(snapshot.LowStockCount));
        AppendCard(rendered, TuiRoutes.Menus, snapshot.MenuCount, Menus(snapshot));
        AppendCard(rendered, TuiRoutes.Orders, snapshot.OrderCount, Orders(snapshot.PendingOrders));
        AppendCard(rendered, TuiRoutes.Audit, snapshot.AuditCount, "Inspect audit logs");
        _ = rendered.AppendLine(CultureInfo.InvariantCulture, $"{TuiRoutes.Tags.Label,-19} Tag any entity");
        _ = rendered.AppendLine();
        _ = rendered.AppendLine("Recent Activity");
        if (snapshot.RecentActivity.Count == 0)
        {
            _ = rendered.AppendLine("No recent activity");
        }
        else
        {
            int available = Math.Max(viewport.Height - 16, 0);
            foreach (DashboardActivity activity in snapshot.RecentActivity.Take(available))
            {
                _ = rendered.AppendLine(CultureInfo.InvariantCulture,
                    $"{activity.Timestamp:HH:mm}  {activity.Actor}  {activity.Action}");
            }
        }

        if (isLoading)
        {
            _ = rendered.AppendLine("Loading dashboard...");
        }

        return rendered.ToString().TrimEnd();
    }

    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        CancellationTokenSource[] cancellations;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            _ = ++generation;
            requestCancellation = null;
            pending = requests.ToArray();
            cancellations = requestSources.ToArray();
        }

        foreach (CancellationTokenSource cancellation in cancellations)
        {
            cancellation.Cancel();
        }

        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
        }
        finally
        {
            foreach (CancellationTokenSource cancellation in cancellations)
            {
                cancellation.Dispose();
            }
        }
    }

    private async Task LoadAsync(long request, CancellationTokenSource cancellation)
    {
        try
        {
            DashboardResult result = await load(cancellation.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || request != generation)
                {
                    return;
                }

                data = result.Data;
                error = result.Error;
                loading = false;
            }

            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync)
            {
                if (!disposed && request == generation)
                {
                    loading = false;
                }
            }

            Changed?.Invoke();
            throw;
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (disposed || request != generation)
                {
                    return;
                }

                error = AppError.Find(exception) ?? AppError.Internal("load TUI dashboard", exception);
                loading = false;
            }

            Changed?.Invoke();
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(requestCancellation, cancellation))
                {
                    requestCancellation = null;
                }
            }
        }
    }

    private static void AppendCard(StringBuilder rendered, TuiRoute route, int count, string description) =>
        _ = rendered.AppendLine(CultureInfo.InvariantCulture,
            $"{route.Label,-19} {FormatCount(count),4}  {description}");

    private static string FormatCount(int count) => count < 0
        ? "?"
        : count.ToString(CultureInfo.InvariantCulture);

    private static string LowStock(int count) => count < 0 ? "Track stock levels" : $"Low stock: {count}";

    private static string Menus(DashboardData value) => value.DraftMenus < 0 || value.PublishedMenus < 0
        ? "Build drink menus"
        : $"Draft {value.DraftMenus} · Published {value.PublishedMenus}";

    private static string Orders(int count) => count < 0 ? "Review orders" : $"Pending: {count}";
}
