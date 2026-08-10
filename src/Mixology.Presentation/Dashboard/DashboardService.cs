using Mixology.Application;
using Mixology.Kernel.Errors;

namespace Mixology.Presentation.Dashboard;

public sealed class DashboardService(ModuleDashboardDataSourceFactory sourceFactory)
{
    public async Task<DashboardResult> LoadAsync(
        MixologySession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await LoadAsync(sourceFactory.Bind(session), cancellationToken).ConfigureAwait(false);
    }

    public static async Task<DashboardResult> LoadAsync(
        IDashboardDataSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Dashboard data = Dashboard.Unknown;
        Exception? first = null;
        Exception? pending = null;

        data = data with
        {
            DrinkCount = await CountAsync(
            "drink count",
            data.DrinkCount,
            source.CountDrinksAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            IngredientCount = await CountAsync(
            "ingredient count",
            data.IngredientCount,
            source.CountIngredientsAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            InventoryCount = await CountAsync(
            "inventory count",
            data.InventoryCount,
            source.CountInventoryAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            LowStockCount = await CountAsync(
            "low-stock count",
            data.LowStockCount,
            source.CountLowStockAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            MenuCount = await CountAsync(
            "menu count",
            data.MenuCount,
            source.CountMenusAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            DraftMenus = await CountAsync(
            "draft-menu count",
            data.DraftMenus,
            source.CountDraftMenusAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            PublishedMenus = await CountAsync(
            "published-menu count",
            data.PublishedMenus,
            source.CountPublishedMenusAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            OrderCount = await CountAsync(
            "order count",
            data.OrderCount,
            source.CountOrdersAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            PendingOrders = await CountAsync(
            "pending-order count",
            data.PendingOrders,
            source.CountPendingOrdersAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();
        data = data with
        {
            AuditCount = await CountAsync(
            "audit count",
            data.AuditCount,
            source.CountAuditAsync,
            cancellationToken).ConfigureAwait(false)
        };
        CaptureError();

        try
        {
            IReadOnlyList<DashboardActivity> recent = await source.RecentActivityAsync(
                Dashboard.RecentActivityLimit,
                cancellationToken).ConfigureAwait(false);
            data = data with { RecentActivity = recent.ToArray() };
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            Capture(Normalize(exception, "load dashboard recent activity"));
        }

        return new DashboardResult(data, first);

        async Task<int> CountAsync(
            string subject,
            int fallback,
            Func<CancellationToken, Task<int>> query,
            CancellationToken token)
        {
            try
            {
                return await query(token).ConfigureAwait(false);
            }
            catch (Exception exception) when (!AppError.IsCancellation(exception))
            {
                pending = Normalize(exception, $"load dashboard {subject}");
                return fallback;
            }
        }

        void CaptureError()
        {
            if (pending is null)
            {
                return;
            }

            Capture(pending);
            pending = null;
        }

        void Capture(Exception exception)
        {
            if (first is null && !AppError.IsPermission(exception))
            {
                first = exception;
            }
        }

        static Exception Normalize(Exception exception, string detail) =>
            AppError.Find(exception) ?? AppError.Internal(detail, exception);
    }
}
