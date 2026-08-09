using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Money;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Menus.Queries;

/// <summary>Owner-defined menu reads and fulfillment policy for collaborating domains.</summary>
public sealed class MenuQueries(IMenuOperations operations, ITagReader tags)
{
    public async Task<Menu> GetAsync(
        StoreSession session,
        MenuId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (id.IsEmpty)
        {
            throw AppError.Invalid("menu id is required");
        }

        _ = MenuId.Parse(id.Value);
        try
        {
            MenuRow? row = await session.Context.Set<MenuRow>()
                .AsNoTracking()
                .Include(static row => row.Items)
                .SingleOrDefaultAsync(
                    row => row.Id == id.Value && row.DeletedAtUtc == null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                throw AppError.NotFound($"menu {id} not found");
            }

            Menu menu = FromRow(row);
            return menu with
            {
                Tags = await tags.ListAsync(
                    session.Context,
                    menu.EntityUid,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (exception is not AppError && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read menu", exception);
        }
    }

    public ValueTask<IReadOnlyList<IngredientFulfillment>?> FulfillIngredientsAsync(
        StoreSession session,
        IReadOnlyList<RecipeIngredient> requirements,
        CancellationToken cancellationToken = default) =>
        operations.FulfillIngredientsAsync(session, requirements, cancellationToken);

    public async Task<IReadOnlySet<string>> ActiveIdsAsync(
        StoreSession session,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ids);
        MenuId[] requested = ids.Distinct(StringComparer.Ordinal).Select(MenuId.Parse).ToArray();
        string[] values = requested.Select(static value => value.Value).ToArray();
        try
        {
            string[] active = await session.Context.Set<MenuRow>()
                .AsNoTracking()
                .Where(row => values.Contains(row.Id) && row.DeletedAtUtc == null)
                .Select(static row => row.Id)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return active.ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read active menu ids", exception);
        }
    }

    private static Menu FromRow(MenuRow row)
    {
        try
        {
            MenuItem[] items = row.Items.OrderBy(static item => item.SortOrder).Select(static item => new MenuItem(
                DrinkId.Parse(item.DrinkId),
                item.DisplayName,
                Price(item),
                item.Featured,
                Availability.Parse(item.Availability),
                item.SortOrder)).ToArray();
            return new Menu(
                MenuId.Parse(row.Id), row.Name, row.Description, items, MenuStatus.Parse(row.Status),
                Utc(row.CreatedAtUtc), row.PublishedAtUtc is { } published ? Utc(published) : null,
                row.DeletedAtUtc is { } deleted ? Utc(deleted) : null, TagCollection.Empty).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted menu {row.Id}", exception);
        }
    }

    private static Price? Price(MenuItemRow row) => row.PriceAmount is null && row.PriceCurrency is null
        ? null
        : row.PriceAmount is { } amount && row.PriceCurrency is { } currency
            ? new Price(amount, Currency.Parse(currency))
            : throw AppError.Internal($"menu item {row.MenuId}/{row.DrinkId} has an incomplete price");

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
