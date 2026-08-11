using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mixology.Application;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Filtering;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Menus.Authorization;
using Mixology.Modules.Menus.Events;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Menus;

public sealed class MenusModule(
    MixologyStore store,
    ITagReader tags,
    IEntityAuthorizer authorizer,
    IMenuOperations operations,
    TimeProvider timeProvider)
{
    public Task<Menu> CreateAsync(
        MixologySession session,
        CreateMenuRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(MenuAuthorization.Create),
            async context =>
            {
                CreateMenuRequest normalized = request.Normalize();
                Menu created = new Menu(
                    MenuId.New(),
                    normalized.Name,
                    normalized.Description,
                    [],
                    MenuStatus.Draft,
                    timeProvider.GetUtcNow().ToUniversalTime(),
                    null,
                    null,
                    TagCollection.Empty,
                    1).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.Create, created).ConfigureAwait(false);
                context.Session!.Context.Add(ToRow(created));
                Record(context, created, new MenuCreated(created));
                return created;
            },
            cancellationToken);
    }

    public Task<Menu> GetAsync(
        MixologySession session,
        MenuId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Query(MenuAuthorization.Get),
            async context =>
            {
                Menu menu = await ReadMenuAsync(id, context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Get, menu).ConfigureAwait(false);
                return menu;
            },
            cancellationToken);
    }

    public Task<Menu> UpdateAsync(
        MixologySession session,
        UpdateMenuRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(MenuAuthorization.Update),
            async context =>
            {
                UpdateMenuRequest normalized = request.Normalize();
                MenuRow row = await RequireActiveRowAsync(context, normalized.Id).ConfigureAwait(false);
                Menu current = await WithTagsAsync(context, FromRow(row)).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Update, current).ConfigureAwait(false);
                current.RequireDraft();
                Menu updated = (current with
                {
                    Name = normalized.Name,
                    Description = normalized.Description.Length == 0
                        ? current.Description
                        : normalized.Description,
                    Revision = checked(normalized.Revision + 1),
                }).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.Update, updated).ConfigureAwait(false);
                context.Session!.Context.ExpectRevision(row, normalized.Revision);
                CopyToRow(updated, row);
                Record(context, updated);
                return updated;
            },
            cancellationToken);
    }

    public Task<Menu> DeleteAsync(
        MixologySession session,
        MenuId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Command(MenuAuthorization.Delete),
            async context =>
            {
                MenuRow row = await RequireActiveRowAsync(context, id).ConfigureAwait(false);
                Menu current = await WithTagsAsync(context, FromRow(row)).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Delete, current).ConfigureAwait(false);
                current.RequireDraft();
                DateTimeOffset deletedAt = timeProvider.GetUtcNow().ToUniversalTime();
                Menu deleted = (current with
                {
                    Status = MenuStatus.Archived,
                    DeletedAt = deletedAt,
                    Revision = checked(current.Revision + 1),
                }).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.Delete, deleted).ConfigureAwait(false);
                CopyToRow(deleted, row);
                Record(context, deleted);
                return deleted;
            },
            cancellationToken);
    }

    public Task<Menu> AddDrinkAsync(
        MixologySession session,
        AddMenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(MenuAuthorization.AddDrink),
            async context =>
            {
                AddMenuItemRequest normalized = request.Normalize();
                await AuthorizePatchAsync(
                    context,
                    MenuAuthorization.AddDrink,
                    normalized.MenuId).ConfigureAwait(false);
                _ = await operations.GetDrinkAsync(
                    context.Session!,
                    normalized.DrinkId,
                    context.CancellationToken).ConfigureAwait(false);
                MenuRow row = await RequireActiveRowAsync(context, normalized.MenuId).ConfigureAwait(false);
                Menu current = await WithTagsAsync(context, FromRow(row)).ConfigureAwait(false);
                current.RequireDraft();
                if (current.Items.Any(item => item.DrinkId == normalized.DrinkId))
                {
                    throw AppError.Invalid($"drink {normalized.DrinkId} is already on menu {current.Id}");
                }

                Availability availability = await CalculateAvailabilityAsync(
                    context.Session!,
                    normalized.DrinkId,
                    context.CancellationToken).ConfigureAwait(false);
                int sortOrder = current.Items.Count == 0
                    ? 0
                    : current.Items.Max(static item => item.SortOrder) + 1;
                MenuItem item = new(
                    normalized.DrinkId,
                    null,
                    null,
                    false,
                    availability,
                    sortOrder);
                Menu updated = (current with
                {
                    Items = [.. current.Items, item],
                    Revision = checked(current.Revision + 1),
                }).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.AddDrink, updated).ConfigureAwait(false);
                CopyToRow(updated, row);
                Record(context, updated, new DrinkAddedToMenu(updated, item));
                context.Touch(normalized.DrinkId.EntityUid);
                return updated;
            },
            cancellationToken);
    }

    public Task<Menu> RemoveDrinkAsync(
        MixologySession session,
        RemoveMenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(MenuAuthorization.RemoveDrink),
            async context =>
            {
                RemoveMenuItemRequest normalized = request.Normalize();
                await AuthorizePatchAsync(
                    context,
                    MenuAuthorization.RemoveDrink,
                    normalized.MenuId).ConfigureAwait(false);
                MenuRow row = await RequireActiveRowAsync(context, normalized.MenuId).ConfigureAwait(false);
                Menu current = await WithTagsAsync(context, FromRow(row)).ConfigureAwait(false);
                current.RequireDraft();
                MenuItem removed = current.Items.SingleOrDefault(item => item.DrinkId == normalized.DrinkId)
                    ?? throw AppError.NotFound($"drink {normalized.DrinkId} is not on menu {current.Id}");
                MenuItem[] remaining = current.Items
                    .Where(item => item.DrinkId != normalized.DrinkId)
                    .ToArray();
                Menu updated = (current with
                {
                    Items = remaining,
                    Revision = checked(current.Revision + 1),
                }).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.RemoveDrink, updated).ConfigureAwait(false);
                CopyToRow(updated, row);
                Record(context, updated, new DrinkRemovedFromMenu(updated, removed));
                context.Touch(normalized.DrinkId.EntityUid);
                return updated;
            },
            cancellationToken);
    }

    public Task<Menu> PublishAsync(
        MixologySession session,
        MenuId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Command(MenuAuthorization.Publish),
            async context =>
            {
                MenuRow row = await RequireActiveRowAsync(context, id).ConfigureAwait(false);
                Menu current = await WithTagsAsync(context, FromRow(row)).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Publish, current).ConfigureAwait(false);
                current.RequirePublishable();
                ReadinessReport report = await operations.GetReadinessAsync(
                    context.Session!,
                    current,
                    context.CancellationToken).ConfigureAwait(false);
                EnsureReportMatches(current, report);
                report.RequireReady();
                List<MenuItem> items = new(current.Items.Count);
                foreach (MenuItem item in current.Items)
                {
                    Availability availability = await CalculateAvailabilityAsync(
                        context.Session!,
                        item.DrinkId,
                        context.CancellationToken).ConfigureAwait(false);
                    items.Add(item with { Availability = availability });
                }

                Menu published = (current with
                {
                    Items = items,
                    Status = MenuStatus.Published,
                    PublishedAt = timeProvider.GetUtcNow().ToUniversalTime(),
                    Revision = checked(current.Revision + 1),
                }).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.Publish, published).ConfigureAwait(false);
                CopyToRow(published, row);
                Record(context, published, new MenuPublished(published));
                return published;
            },
            cancellationToken);
    }

    public Task<Menu> DraftAsync(
        MixologySession session,
        MenuId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Command(MenuAuthorization.Draft),
            async context =>
            {
                MenuRow row = await RequireActiveRowAsync(context, id).ConfigureAwait(false);
                Menu current = await WithTagsAsync(context, FromRow(row)).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Draft, current).ConfigureAwait(false);
                current.RequireReturnToDraft();
                Menu drafted = (current with
                {
                    Status = MenuStatus.Draft,
                    PublishedAt = null,
                    Revision = checked(current.Revision + 1),
                }).Normalize();
                await AuthorizeAsync(context, MenuAuthorization.Draft, drafted).ConfigureAwait(false);
                CopyToRow(drafted, row);
                Record(context, drafted, new MenuDrafted(drafted));
                return drafted;
            },
            cancellationToken);
    }

    public Task<ReadinessReport> ReadinessAsync(
        MixologySession session,
        MenuId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Query(MenuAuthorization.Readiness),
            async context =>
            {
                await using StoreSession read = await store.OpenSessionAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                Menu menu = await ReadMenuAsync(read, id, context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Readiness, menu).ConfigureAwait(false);
                ReadinessReport report = await operations.GetReadinessAsync(
                    read,
                    menu,
                    context.CancellationToken).ConfigureAwait(false);
                EnsureReportMatches(menu, report);
                return report;
            },
            cancellationToken);
    }

    public Task<MenuAnalysis> AnalyzeAsync(
        MixologySession session,
        MenuId id,
        double targetMargin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        if (targetMargin is <= 0 or >= 1)
        {
            throw AppError.Invalid("target margin must be between 0 and 1");
        }

        return session.ExecuteAsync(
            Query(MenuAuthorization.Readiness),
            async context =>
            {
                await using StoreSession read = await store.OpenSessionAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                Menu menu = await ReadMenuAsync(read, id, context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, MenuAuthorization.Readiness, menu).ConfigureAwait(false);
                MenuAnalysis analysis = await operations.AnalyzeAsync(
                    read,
                    menu,
                    targetMargin,
                    context.CancellationToken).ConfigureAwait(false);
                if (analysis.Menu.Id != menu.Id)
                {
                    throw AppError.Internal("menu analysis returned a different menu");
                }

                return analysis;
            },
            cancellationToken);
    }

    public Task<Page<Menu>> ListAsync(
        MixologySession session,
        ListMenusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListMenusRequest normalized = request.Normalize();
        FilterExpression<MenuFilter>? expression = Filter.Parse(MenuFilter.Schema, normalized.Filter);
        return session.ExecuteAsync(
            Query(MenuAuthorization.List),
            context => ListCoreAsync(context, normalized, expression),
            cancellationToken);
    }

    public async Task<int> CountAsync(
        MixologySession session,
        ListMenusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ListMenusRequest normalized = request.Normalize() with
        {
            Cursor = default,
            Limit = PageRequest.DefaultLimit,
        };
        return await Paging.CountAsync<Menu>(
            async (cursor, token) => await ListAsync(session, normalized with { Cursor = cursor }, token)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Page<Menu>> ListCoreAsync(
        OperationContext context,
        ListMenusRequest request,
        FilterExpression<MenuFilter>? expression)
    {
        (MenuRow[] Rows, IReadOnlyDictionary<EntityUid, TagCollection> Tags) data = await ReadAsync(
            async database =>
            {
                IQueryable<MenuRow> query = MenuRows(database).Where(static row => row.DeletedAtUtc == null);
                if (request.Status is { } status)
                {
                    string value = status.Value;
                    query = query.Where(row => row.Status == value);
                }

                Expression<Func<MenuRow, bool>>? pushdown = expression?.BuildPushdown(MenuFilter.Persistence);
                if (pushdown is not null)
                {
                    query = query.Where(pushdown);
                }

                MenuRow[] rows = await query.OrderByDescending(static row => row.Id)
                    .ToArrayAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                IReadOnlyDictionary<EntityUid, TagCollection> loadedTags = await tags.ListTypeAsync(
                    database,
                    EntityIds.MenuType,
                    rows.Select(static row => row.Id).ToArray(),
                    context.CancellationToken).ConfigureAwait(false);
                return (rows, loadedTags);
            },
            context.CancellationToken).ConfigureAwait(false);

        if (!request.Cursor.IsEmpty)
        {
            data.Rows = data.Rows
                .Where(row => string.CompareOrdinal(row.Id, request.Cursor.Value) < 0)
                .ToArray();
        }

        List<Menu> visible = [];
        foreach (MenuRow row in data.Rows)
        {
            Menu menu = FromRow(row);
            if (data.Tags.TryGetValue(menu.EntityUid, out TagCollection? loadedTags))
            {
                menu = menu with { Tags = loadedTags };
            }
            if (expression is not null && !expression.Match(ToFilter(menu)))
            {
                continue;
            }

            try
            {
                await AuthorizeAsync(context, MenuAuthorization.List, menu).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                AppError.IsPermission(exception) && !AppError.IsCancellation(exception))
            {
                continue;
            }

            visible.Add(menu);
            if (visible.Count > request.Limit)
            {
                break;
            }
        }

        bool hasNext = visible.Count > request.Limit;
        if (hasNext)
        {
            visible.RemoveAt(visible.Count - 1);
        }

        Cursor next = hasNext ? new Cursor(visible[^1].Id.Value) : default;
        return new Page<Menu>(visible, next);
    }

    private async Task<Menu> ReadMenuAsync(MenuId id, CancellationToken cancellationToken) =>
        await ReadAsync(
            database => ReadMenuAsync(database, id, cancellationToken),
            cancellationToken).ConfigureAwait(false);

    private Task<Menu> ReadMenuAsync(
        StoreSession session,
        MenuId id,
        CancellationToken cancellationToken) =>
        ReadMenuAsync(session.Context, id, cancellationToken);

    private async Task<Menu> ReadMenuAsync(
        MixologyDbContext database,
        MenuId id,
        CancellationToken cancellationToken)
    {
        MenuRow? row = await MenuRows(database)
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
            Tags = await tags.ListAsync(database, menu.EntityUid, cancellationToken).ConfigureAwait(false),
        };
    }

    private static IQueryable<MenuRow> MenuRows(MixologyDbContext database) =>
        database.Set<MenuRow>().AsNoTracking().Include(static row => row.Items);

    private async Task<Menu> WithTagsAsync(OperationContext context, Menu menu) =>
        menu with
        {
            Tags = await tags.ListAsync(
                context.Session!.Context,
                menu.EntityUid,
                context.CancellationToken).ConfigureAwait(false),
        };

    private static async Task<MenuRow> RequireActiveRowAsync(OperationContext context, MenuId id)
    {
        MenuRow? row = await context.Session!.Context.Set<MenuRow>()
            .Include(static row => row.Items)
            .SingleOrDefaultAsync(
                row => row.Id == id.Value && row.DeletedAtUtc == null,
                context.CancellationToken)
            .ConfigureAwait(false);
        return row ?? throw AppError.NotFound($"menu {id} not found");
    }

    private ValueTask AuthorizeAsync(OperationContext context, EntityUid action, Menu menu) =>
        authorizer.AuthorizeAsync(context.Principal, action, menu.ToCedarEntity(), context.CancellationToken);

    private ValueTask AuthorizePatchAsync(OperationContext context, EntityUid action, MenuId menuId) =>
        authorizer.AuthorizeAsync(
            context.Principal,
            action,
            new MenuAuthorizationResource(
                menuId.EntityUid,
                new Dictionary<string, string>(StringComparer.Ordinal),
                string.Empty,
                string.Empty).ToCedarEntity(),
            context.CancellationToken);

    private async ValueTask<Availability> CalculateAvailabilityAsync(
        StoreSession session,
        DrinkId drinkId,
        CancellationToken cancellationToken)
    {
        try
        {
            Availability availability = await operations.GetAvailabilityAsync(
                session,
                drinkId,
                cancellationToken).ConfigureAwait(false);
            availability.Validate();
            return availability;
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            return Availability.Unavailable;
        }
    }

    private async Task<TResult> ReadAsync<TResult>(
        Func<MixologyDbContext, Task<TResult>> query,
        CancellationToken cancellationToken)
    {
        try
        {
            await using StoreSession read = await store.OpenSessionAsync(cancellationToken).ConfigureAwait(false);
            return await query(read.Context).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read menus", exception);
        }
    }

    private static Menu FromRow(MenuRow row)
    {
        try
        {
            MenuItem[] items = row.Items
                .OrderBy(static item => item.SortOrder)
                .Select(static item => new MenuItem(
                    DrinkId.Parse(item.DrinkId),
                    item.DisplayName,
                    ToPrice(item),
                    item.Featured,
                    Availability.Parse(item.Availability),
                    item.SortOrder))
                .ToArray();
            return new Menu(
                MenuId.Parse(row.Id),
                row.Name,
                row.Description,
                items,
                MenuStatus.Parse(row.Status),
                Utc(row.CreatedAtUtc),
                row.PublishedAtUtc is { } published ? Utc(published) : null,
                row.DeletedAtUtc is { } deleted ? Utc(deleted) : null,
                TagCollection.Empty,
                row.Revision).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted menu {row.Id}", exception);
        }
    }

    private static MenuRow ToRow(Menu menu)
    {
        MenuRow row = new()
        {
            Id = menu.Id.Value,
            Name = menu.Name,
            Description = menu.Description,
            Status = menu.Status.Value,
            CreatedAtUtc = menu.CreatedAt.UtcDateTime,
            PublishedAtUtc = menu.PublishedAt?.UtcDateTime,
            DeletedAtUtc = menu.DeletedAt?.UtcDateTime,
            Revision = menu.Revision,
        };
        AddItems(menu, row);
        return row;
    }

    private static void CopyToRow(Menu menu, MenuRow row)
    {
        row.Name = menu.Name;
        row.Description = menu.Description;
        row.Status = menu.Status.Value;
        row.CreatedAtUtc = menu.CreatedAt.UtcDateTime;
        row.PublishedAtUtc = menu.PublishedAt?.UtcDateTime;
        row.DeletedAtUtc = menu.DeletedAt?.UtcDateTime;
        HashSet<string> retained = menu.Items.Select(static item => item.DrinkId.Value)
            .ToHashSet(StringComparer.Ordinal);
        row.Items.RemoveAll(item => !retained.Contains(item.DrinkId));
        foreach (MenuItem item in menu.Items)
        {
            MenuItemRow? persisted = row.Items.SingleOrDefault(existing => existing.DrinkId == item.DrinkId.Value);
            if (persisted is null)
            {
                row.Items.Add(ToRow(menu.Id, item));
                continue;
            }

            persisted.DisplayName = item.DisplayName;
            persisted.PriceAmount = item.Price?.Amount;
            persisted.PriceCurrency = item.Price?.Currency.Code;
            persisted.Featured = item.Featured;
            persisted.Availability = item.Availability.Value;
            persisted.SortOrder = item.SortOrder;
        }
    }

    private static void AddItems(Menu menu, MenuRow row)
    {
        foreach (MenuItem item in menu.Items)
        {
            row.Items.Add(ToRow(menu.Id, item));
        }
    }

    private static MenuItemRow ToRow(MenuId menuId, MenuItem item) => new()
    {
        MenuId = menuId.Value,
        DrinkId = item.DrinkId.Value,
        DisplayName = item.DisplayName,
        PriceAmount = item.Price?.Amount,
        PriceCurrency = item.Price?.Currency.Code,
        Featured = item.Featured,
        Availability = item.Availability.Value,
        SortOrder = item.SortOrder,
    };

    private static Price? ToPrice(MenuItemRow row)
    {
        if (row.PriceAmount is null && row.PriceCurrency is null)
        {
            return null;
        }

        if (row.PriceAmount is not { } amount || row.PriceCurrency is not { } currency)
        {
            throw AppError.Internal($"menu item {row.MenuId}/{row.DrinkId} has an incomplete price");
        }

        return new Price(amount, Currency.Parse(currency));
    }

    private static MenuFilter ToFilter(Menu menu) => new(
        menu.Id.Value,
        menu.Name,
        menu.Description,
        menu.Status.Value,
        menu.CreatedAt.UtcDateTime,
        menu.Tags.Strings().ToArray());

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static void EnsureReportMatches(Menu menu, ReadinessReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.MenuId != menu.Id || report.Status != menu.Status)
        {
            throw AppError.Internal("menu readiness report does not match the requested menu state");
        }
    }

    private static void Record(OperationContext context, Menu menu, object? domainEvent = null)
    {
        context.SelectResource(menu.EntityUid);
        context.Touch(menu.EntityUid);
        if (domainEvent is not null)
        {
            context.AddEvent(domainEvent);
        }
    }

    private static void RequireId(MenuId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("menu id is required");
        }

        _ = MenuId.Parse(id.Value);
    }

    private static Operation Command(EntityUid action) => Operation.Command(ActionName(action));
    private static Operation Query(EntityUid action) => Operation.Query(ActionName(action));
    private static string ActionName(EntityUid action) => $"{action.Type}::\"{action.Id}\"";
}
