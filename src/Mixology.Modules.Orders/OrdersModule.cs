using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mixology.Application;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Filtering;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Queries;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Events;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Persistence;
using Mixology.Modules.Orders.Requests;
using Mixology.Persistence;

namespace Mixology.Modules.Orders;

public sealed class OrdersModule(
    MixologyStore store,
    IEntityAuthorizer authorizer,
    MenuQueries menus,
    DrinkQueries drinks,
    IngredientQueries ingredients,
    TimeProvider timeProvider)
{
    public Task<Order> PlaceAsync(
        MixologySession session,
        PlaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(OrderAuthorization.Place),
            async context =>
            {
                PlaceOrderRequest normalized = request.Normalize();
                OrderItem[] items = normalized.Items.Select(static item => item.Normalize()).ToArray();
                DateTimeOffset createdAt = timeProvider.GetUtcNow().ToUniversalTime();
                Order order = new(
                    OrderId.New(),
                    normalized.MenuId,
                    items,
                    [],
                    [],
                    OrderStatus.Pending,
                    createdAt,
                    null,
                    normalized.Notes,
                    null,
                    TagCollection.Empty);

                // The Go middleware authorizes the proposed pending order before dependency reads.
                await AuthorizeAsync(context, OrderAuthorization.Place, order).ConfigureAwait(false);
                StoreSession active = context.Session!;
                Menu menu = await menus.GetAsync(
                    active,
                    normalized.MenuId,
                    context.CancellationToken).ConfigureAwait(false);
                if (menu.Status != MenuStatus.Published)
                {
                    throw AppError.FailedPrecondition($"menu {menu.Id} is not published");
                }

                HashSet<DrinkId> menuDrinks = menu.Items.Select(static item => item.DrinkId).ToHashSet();
                List<RecipeIngredient> requirements = [];
                foreach (OrderItem item in items)
                {
                    if (!menuDrinks.Contains(item.DrinkId))
                    {
                        throw AppError.NotFound($"drink {item.DrinkId} is not on menu {menu.Id}");
                    }

                    Drink drink = await drinks.GetAsync(
                        active,
                        item.DrinkId,
                        context.CancellationToken).ConfigureAwait(false);
                    requirements.AddRange(drink.Recipe.Ingredients
                        .Where(static requirement => !requirement.Optional)
                        .Select(requirement => new RecipeIngredient(
                            requirement.IngredientId,
                            requirement.Amount.Multiply(item.Quantity),
                            false,
                            requirement.Substitutes)));
                }

                IReadOnlyList<IngredientFulfillment>? fulfilled = await menus.FulfillIngredientsAsync(
                    active,
                    requirements,
                    context.CancellationToken).ConfigureAwait(false);
                if (fulfilled is null)
                {
                    throw AppError.Invalid("insufficient stock to fulfill order");
                }

                Dictionary<IngredientId, IngredientUsage> usage = [];
                foreach (IngredientFulfillment selected in fulfilled)
                {
                    string name = (await ingredients.GetAsync(
                        active,
                        selected.IngredientId,
                        context.CancellationToken).ConfigureAwait(false)).Name;
                    usage[selected.IngredientId] = usage.TryGetValue(selected.IngredientId, out IngredientUsage? current)
                        ? current with { Amount = current.Amount.Add(selected.Required) }
                        : new IngredientUsage(selected.IngredientId, name, selected.Required);
                }

                order = (order with
                {
                    IngredientUsage = usage.Values
                        .OrderBy(static value => value.IngredientId.Value, StringComparer.Ordinal)
                        .ToArray(),
                }).Normalize();
                active.Context.Add(ToRow(order));
                Record(context, order, new OrderPlaced(order));
                return order;
            },
            cancellationToken);
    }

    public Task<Order> GetAsync(
        MixologySession session,
        OrderId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Query(OrderAuthorization.Get),
            async context =>
            {
                Order order = await ReadAsync(
                    async database =>
                    {
                        OrderRow? row = await Rows(database).AsNoTracking().SingleOrDefaultAsync(
                            candidate => candidate.Id == id.Value && candidate.DeletedAtUtc == null,
                            context.CancellationToken).ConfigureAwait(false);
                        return row is null
                            ? throw AppError.NotFound($"order {id} not found")
                            : FromRow(row);
                    },
                    context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, OrderAuthorization.Get, order).ConfigureAwait(false);
                return order;
            },
            cancellationToken);
    }

    public Task<Page<Order>> ListAsync(
        MixologySession session,
        ListOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListOrdersRequest normalized = request.Normalize();
        FilterExpression<OrderFilter>? expression = Filter.Parse(OrderFilter.Schema, normalized.Filter);
        return session.ExecuteAsync(
            Query(OrderAuthorization.List),
            context => ListCoreAsync(context, normalized, expression),
            cancellationToken);
    }

    public async Task<int> CountAsync(
        MixologySession session,
        ListOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ListOrdersRequest normalized = request.Normalize() with
        {
            Cursor = default,
            Limit = PageRequest.DefaultLimit,
        };
        return await Paging.CountAsync<Order>(
            async (cursor, token) => await ListAsync(
                session,
                normalized with { Cursor = cursor },
                token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Order> CompleteAsync(
        MixologySession session,
        OrderId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Command(OrderAuthorization.Complete),
            async context =>
            {
                OrderRow row = await RequireActiveRowAsync(context, id).ConfigureAwait(false);
                Order current = FromRow(row);
                await AuthorizeAsync(context, OrderAuthorization.Complete, current).ConfigureAwait(false);
                if (current.Status == OrderStatus.Completed)
                {
                    return current;
                }

                if (current.Status == OrderStatus.Cancelled)
                {
                    throw AppError.Invalid($"order {id} is cancelled");
                }

                if (current.Status == OrderStatus.Blocked)
                {
                    throw AppError.Invalid($"order {id} is blocked by insufficient reserved stock");
                }

                Order completed = (current with
                {
                    Status = OrderStatus.Completed,
                    CompletedAt = timeProvider.GetUtcNow().ToUniversalTime(),
                }).Normalize();
                CopyLifecycleToRow(completed, row);
                Record(context, completed, new OrderCompleted(completed));
                return completed;
            },
            cancellationToken);
    }

    public Task<Order> CancelAsync(
        MixologySession session,
        OrderId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(id);
        return session.ExecuteAsync(
            Command(OrderAuthorization.Cancel),
            async context =>
            {
                OrderRow row = await RequireActiveRowAsync(context, id).ConfigureAwait(false);
                Order current = FromRow(row);
                await AuthorizeAsync(context, OrderAuthorization.Cancel, current).ConfigureAwait(false);
                if (current.Status == OrderStatus.Cancelled)
                {
                    return current;
                }

                if (current.Status == OrderStatus.Completed)
                {
                    throw AppError.Invalid($"order {id} is already completed");
                }

                Order cancelled = (current with
                {
                    Status = OrderStatus.Cancelled,
                    CompletedAt = null,
                }).Normalize();
                CopyLifecycleToRow(cancelled, row);
                Record(context, cancelled, new OrderCancelled(cancelled));
                return cancelled;
            },
            cancellationToken);
    }

    private async Task<Page<Order>> ListCoreAsync(
        OperationContext context,
        ListOrdersRequest request,
        FilterExpression<OrderFilter>? expression)
    {
        OrderRow[] rows = await ReadAsync(
            async database =>
            {
                IQueryable<OrderRow> query = Rows(database).AsNoTracking()
                    .Where(static row => row.DeletedAtUtc == null);
                if (request.Status is { } status)
                {
                    query = query.Where(row => row.Status == status.Value);
                }

                if (request.MenuId is { } menuId)
                {
                    query = query.Where(row => row.MenuId == menuId.Value);
                }

                Expression<Func<OrderRow, bool>>? pushdown = expression?.BuildPushdown(OrderFilter.Persistence);
                if (pushdown is not null)
                {
                    query = query.Where(pushdown);
                }

                return await query.OrderByDescending(static row => row.Id)
                    .ToArrayAsync(context.CancellationToken).ConfigureAwait(false);
            },
            context.CancellationToken).ConfigureAwait(false);

        if (!request.Cursor.IsEmpty)
        {
            rows = rows.Where(row => string.CompareOrdinal(row.Id, request.Cursor.Value) < 0).ToArray();
        }

        List<Order> visible = [];
        foreach (OrderRow row in rows)
        {
            Order order = FromRow(row);
            if (expression is not null && !expression.Match(ToFilter(order)))
            {
                continue;
            }

            try
            {
                await AuthorizeAsync(context, OrderAuthorization.List, order).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                AppError.IsPermission(exception) && !AppError.IsCancellation(exception))
            {
                continue;
            }

            visible.Add(order);
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

        return new Page<Order>(
            visible,
            hasNext ? new Cursor(visible[^1].Id.Value) : default);
    }

    private static IQueryable<OrderRow> Rows(MixologyDbContext database) =>
        database.Set<OrderRow>()
            .Include(static row => row.Items)
            .Include(static row => row.IngredientUsage)
            .Include(static row => row.BlockedIngredients)
            .AsSplitQuery();

    private static async Task<OrderRow> RequireActiveRowAsync(
        OperationContext context,
        OrderId id)
    {
        OrderRow? row = await Rows(context.Session!.Context).SingleOrDefaultAsync(
            candidate => candidate.Id == id.Value && candidate.DeletedAtUtc == null,
            context.CancellationToken).ConfigureAwait(false);
        return row ?? throw AppError.NotFound($"order {id} not found");
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
            throw AppError.Internal("read orders", exception);
        }
    }

    private ValueTask AuthorizeAsync(OperationContext context, EntityUid action, Order order) =>
        authorizer.AuthorizeAsync(
            context.Principal,
            action,
            order.ToCedarEntity(),
            context.CancellationToken);

    private static Order FromRow(OrderRow row)
    {
        try
        {
            return new Order(
                OrderId.Parse(row.Id),
                MenuId.Parse(row.MenuId),
                row.Items.OrderBy(static value => value.Position).Select(static value => new OrderItem(
                    DrinkId.Parse(value.DrinkId),
                    value.Quantity,
                    value.Notes)).ToArray(),
                row.IngredientUsage.OrderBy(static value => value.Position).Select(static value => new IngredientUsage(
                    IngredientId.Parse(value.IngredientId),
                    value.Name,
                    Amount.Create(value.Quantity, Unit.Parse(value.Unit)))).ToArray(),
                row.BlockedIngredients.Select(static value => IngredientId.Parse(value.IngredientId)).ToArray(),
                OrderStatus.Parse(row.Status),
                Utc(row.CreatedAtUtc),
                row.CompletedAtUtc is { } completed ? Utc(completed) : null,
                row.Notes,
                row.DeletedAtUtc is { } deleted ? Utc(deleted) : null,
                TagCollection.Empty).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted order {row.Id}", exception);
        }
    }

    private static OrderRow ToRow(Order order)
    {
        OrderRow row = new()
        {
            Id = order.Id.Value,
            MenuId = order.MenuId.Value,
            Status = order.Status.Value,
            CreatedAtUtc = order.CreatedAt.UtcDateTime,
            CompletedAtUtc = order.CompletedAt?.UtcDateTime,
            Notes = order.Notes,
            DeletedAtUtc = order.DeletedAt?.UtcDateTime,
        };
        for (int position = 0; position < order.Items.Count; position++)
        {
            OrderItem item = order.Items[position];
            row.Items.Add(new OrderItemRow
            {
                OrderId = order.Id.Value,
                Position = position,
                DrinkId = item.DrinkId.Value,
                Quantity = item.Quantity,
                Notes = item.Notes,
            });
        }

        for (int position = 0; position < order.IngredientUsage.Count; position++)
        {
            IngredientUsage usage = order.IngredientUsage[position];
            row.IngredientUsage.Add(new OrderIngredientUsageRow
            {
                OrderId = order.Id.Value,
                Position = position,
                IngredientId = usage.IngredientId.Value,
                Name = usage.Name,
                Quantity = usage.Amount.Value,
                Unit = usage.Amount.Unit.Value,
            });
        }

        foreach (IngredientId ingredientId in order.BlockedIngredientIds)
        {
            row.BlockedIngredients.Add(new OrderBlockedIngredientRow
            {
                OrderId = order.Id.Value,
                IngredientId = ingredientId.Value,
            });
        }

        return row;
    }

    private static void CopyLifecycleToRow(Order order, OrderRow row)
    {
        row.Status = order.Status.Value;
        row.CompletedAtUtc = order.CompletedAt?.UtcDateTime;
    }

    private static OrderFilter ToFilter(Order order) => new(
        order.Id.Value,
        order.MenuId.Value,
        order.Status.Value,
        order.CreatedAt.UtcDateTime,
        order.Notes,
        order.Tags.Strings().ToArray());

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static void Record(OperationContext context, Order order, object domainEvent)
    {
        context.SelectResource(order.EntityUid);
        context.Touch(order.EntityUid);
        context.AddEvent(domainEvent);
    }

    private static void RequireId(OrderId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("order id is required");
        }

        _ = OrderId.Parse(id.Value);
    }

    private static Operation Command(EntityUid action) => Operation.Command(ActionName(action));
    private static Operation Query(EntityUid action) => Operation.Query(ActionName(action));
    private static string ActionName(EntityUid action) => $"{action.Type}::\"{action.Id}\"";
}
