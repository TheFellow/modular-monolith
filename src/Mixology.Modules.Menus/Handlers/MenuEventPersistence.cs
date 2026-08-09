using Microsoft.EntityFrameworkCore;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Modules.Menus.Persistence;

namespace Mixology.Modules.Menus.Handlers;

internal static class MenuEventPersistence
{
    public static Task<MenuRow[]> LoadByDrinkIdsAsync(
        EventHandlerContext context,
        IReadOnlyCollection<string> drinkIds,
        bool publishedOnly = false,
        bool tracking = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(drinkIds);
        if (drinkIds.Count == 0)
        {
            return Task.FromResult(Array.Empty<MenuRow>());
        }

        string[] ids = drinkIds.Distinct(StringComparer.Ordinal).ToArray();
        return LoadAsync(
            context,
            query => query.Where(row => row.Items.Any(item => ids.Contains(item.DrinkId))),
            publishedOnly,
            tracking,
            "load menus by drink");
    }

    public static Task<MenuRow[]> LoadByIdsAsync(
        EventHandlerContext context,
        IReadOnlyCollection<string> menuIds,
        bool tracking = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(menuIds);
        if (menuIds.Count == 0)
        {
            return Task.FromResult(Array.Empty<MenuRow>());
        }

        string[] ids = menuIds.Distinct(StringComparer.Ordinal).ToArray();
        return LoadAsync(
            context,
            query => query.Where(row => ids.Contains(row.Id)),
            publishedOnly: false,
            tracking,
            "load prepared menus");
    }

    private static async Task<MenuRow[]> LoadAsync(
        EventHandlerContext context,
        Func<IQueryable<MenuRow>, IQueryable<MenuRow>> filter,
        bool publishedOnly,
        bool tracking,
        string failure)
    {
        try
        {
            IQueryable<MenuRow> query = context.Session.Context.Set<MenuRow>()
                .Include(static row => row.Items)
                .Where(static row => row.DeletedAtUtc == null);
            if (publishedOnly)
            {
                query = query.Where(static row => row.Status == "published");
            }

            query = filter(query);
            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            return await query.OrderBy(static row => row.Id)
                .ToArrayAsync(context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal(failure, exception);
        }
    }
}
