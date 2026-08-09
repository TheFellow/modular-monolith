using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mixology.Application;
using Mixology.Application.Operations;
using Mixology.Authorization.Cedar;
using Mixology.Filtering;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Inventory.Authorization;
using Mixology.Modules.Inventory.Events;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Inventory;

public sealed class InventoryModule(
    MixologyStore store,
    ITagReader tags,
    IEntityAuthorizer authorizer,
    TimeProvider timeProvider)
{
    public Task<InventoryStock> GetAsync(
        MixologySession session,
        IngredientId ingredientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireIngredientId(ingredientId);
        return session.ExecuteAsync(
            Query(InventoryAuthorization.Get),
            async context =>
            {
                InventoryStock inventory = await ReadAsync(
                    async database =>
                    {
                        InventoryRow? row = await database.Set<InventoryRow>()
                            .AsNoTracking()
                            .SingleOrDefaultAsync(
                                candidate => candidate.IngredientId == ingredientId.Value,
                                context.CancellationToken)
                            .ConfigureAwait(false);
                        if (row is null)
                        {
                            throw AppError.NotFound($"stock for ingredient {ingredientId} not found");
                        }

                        InventoryReservationRow[] reservations = await database.Set<InventoryReservationRow>()
                            .AsNoTracking()
                            .Where(candidate => candidate.IngredientId == ingredientId.Value)
                            .ToArrayAsync(context.CancellationToken)
                            .ConfigureAwait(false);
                        InventoryStock loaded = FromRows(row, reservations);
                        return loaded with
                        {
                            Tags = await tags.ListAsync(
                                database,
                                loaded.EntityUid,
                                context.CancellationToken).ConfigureAwait(false),
                        };
                    },
                    context.CancellationToken).ConfigureAwait(false);
                await AuthorizeAsync(context, InventoryAuthorization.Get, inventory).ConfigureAwait(false);
                return inventory;
            },
            cancellationToken);
    }

    public Task<Page<InventoryStock>> ListAsync(
        MixologySession session,
        ListInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ListInventoryRequest normalized = request.Normalize();
        FilterExpression<InventoryFilter>? expression = Filter.Parse(InventoryFilter.Schema, normalized.Filter);
        return session.ExecuteAsync(
            Query(InventoryAuthorization.List),
            context => ListCoreAsync(context, normalized, expression),
            cancellationToken);
    }

    public async Task<int> CountAsync(
        MixologySession session,
        ListInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ListInventoryRequest normalized = request.Normalize() with
        {
            Cursor = default,
            Limit = PageRequest.DefaultLimit,
        };
        return await Paging.CountAsync<InventoryStock>(
            async (cursor, token) => await ListAsync(
                session,
                normalized with { Cursor = cursor },
                token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<InventoryStock> SetAsync(
        MixologySession session,
        SetInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(InventoryAuthorization.Set),
            async context =>
            {
                SetInventoryRequest normalized = request.Normalize();
                InventoryRow? row = await context.Session!.Context.Set<InventoryRow>()
                    .SingleOrDefaultAsync(
                        candidate => candidate.IngredientId == normalized.IngredientId.Value,
                        context.CancellationToken)
                    .ConfigureAwait(false);
                InventoryReservationRow[] reservations = await ReservationsAsync(
                    context,
                    normalized.IngredientId).ConfigureAwait(false);
                InventoryId id = row is null ? InventoryId.New() : InventoryId.Parse(row.Id);
                TagCollection currentTags = row is null
                    ? TagCollection.Empty
                    : await tags.ListAsync(
                        context.Session.Context,
                        id.EntityUid,
                        context.CancellationToken).ConfigureAwait(false);
                Amount onHand = normalized.OnHand.Value < 0d
                    ? Amount.Create(0d, normalized.OnHand.Unit)
                    : normalized.OnHand;
                Amount reserved = ReservedAmount(onHand.Unit, reservations);
                InventoryStock updated = new InventoryStock(
                    id,
                    normalized.IngredientId,
                    onHand,
                    reserved,
                    normalized.UnitCost,
                    timeProvider.GetUtcNow(),
                    currentTags).Normalize();
                await AuthorizeAsync(context, InventoryAuthorization.Set, updated).ConfigureAwait(false);

                if (row is null)
                {
                    row = ToRow(updated);
                    context.Session.Context.Add(row);
                }
                else
                {
                    CopyToRow(updated, row);
                }

                RecordAdjustment(context, updated, "set");
                return updated;
            },
            cancellationToken);
    }

    public Task<InventoryStock> AdjustAsync(
        MixologySession session,
        AdjustInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return session.ExecuteAsync(
            Command(InventoryAuthorization.Adjust),
            async context =>
            {
                AdjustInventoryRequest normalized = request.Normalize();
                InventoryRow? row = await context.Session!.Context.Set<InventoryRow>()
                    .SingleOrDefaultAsync(
                        candidate => candidate.IngredientId == normalized.IngredientId.Value,
                        context.CancellationToken)
                    .ConfigureAwait(false);
                if (row is null && normalized.Delta is null)
                {
                    throw AppError.NotFound($"stock for ingredient {normalized.IngredientId} not found");
                }

                Amount current = row is null
                    ? Amount.Create(0d, normalized.Delta!.Unit)
                    : PersistedAmount(row.Quantity, row.Unit, $"inventory {row.Id}");
                Amount next = current;
                if (normalized.Delta is { } delta)
                {
                    next = current.Add(delta.Convert(current.Unit));
                    if (next.Value < 0d)
                    {
                        next = Amount.Create(0d, current.Unit);
                    }
                }

                Price? cost = normalized.UnitCost ?? (row is null ? null : PersistedPrice(row));
                InventoryReservationRow[] reservations = await ReservationsAsync(
                    context,
                    normalized.IngredientId).ConfigureAwait(false);
                InventoryId id = row is null ? InventoryId.New() : InventoryId.Parse(row.Id);
                TagCollection currentTags = row is null
                    ? TagCollection.Empty
                    : await tags.ListAsync(
                        context.Session.Context,
                        id.EntityUid,
                        context.CancellationToken).ConfigureAwait(false);
                InventoryStock updated = new(
                    id,
                    normalized.IngredientId,
                    next,
                    ReservedAmount(next.Unit, reservations),
                    cost,
                    timeProvider.GetUtcNow(),
                    currentTags);
                updated = updated.Normalize();
                await AuthorizeAsync(context, InventoryAuthorization.Adjust, updated).ConfigureAwait(false);

                if (row is null)
                {
                    row = ToRow(updated);
                    context.Session.Context.Add(row);
                }
                else
                {
                    CopyToRow(updated, row);
                }

                if (normalized.Delta is not null)
                {
                    RecordAdjustment(context, updated, normalized.Reason.Value);
                }
                else
                {
                    context.SelectResource(updated.EntityUid);
                    context.Touch(updated.EntityUid);
                }

                return updated;
            },
            cancellationToken);
    }

    private async Task<Page<InventoryStock>> ListCoreAsync(
        OperationContext context,
        ListInventoryRequest request,
        FilterExpression<InventoryFilter>? expression)
    {
        (InventoryRow[] Rows, InventoryReservationRow[] Reservations,
            IReadOnlyDictionary<EntityUid, TagCollection> Tags) data = await ReadAsync(
            async database =>
            {
                IQueryable<InventoryRow> query = database.Set<InventoryRow>().AsNoTracking();
                if (request.IngredientId is { } ingredientId)
                {
                    query = query.Where(row => row.IngredientId == ingredientId.Value);
                }

                if (request.LowStock is { } maximum)
                {
                    query = query.Where(row => row.Quantity <= maximum);
                }

                Expression<Func<InventoryRow, bool>>? pushdown = expression?.BuildPushdown(InventoryFilter.Persistence);
                if (pushdown is not null)
                {
                    query = query.Where(pushdown);
                }

                InventoryRow[] rows = await query.OrderByDescending(static row => row.Id)
                    .ToArrayAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                string[] ingredientIds = rows.Select(static row => row.IngredientId).Distinct().ToArray();
                InventoryReservationRow[] reservations = await database.Set<InventoryReservationRow>()
                    .AsNoTracking()
                    .Where(row => ingredientIds.Contains(row.IngredientId))
                    .ToArrayAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                IReadOnlyDictionary<EntityUid, TagCollection> loadedTags = await tags.ListTypeAsync(
                    database,
                    EntityIds.InventoryType,
                    rows.Select(static row => row.Id).ToArray(),
                    context.CancellationToken).ConfigureAwait(false);
                return (rows, reservations, loadedTags);
            },
            context.CancellationToken).ConfigureAwait(false);

        if (!request.Cursor.IsEmpty)
        {
            data.Rows = data.Rows
                .Where(row => string.CompareOrdinal(row.Id, request.Cursor.Value) < 0)
                .ToArray();
        }

        ILookup<string, InventoryReservationRow> reservationsByIngredient =
            data.Reservations.ToLookup(static row => row.IngredientId, StringComparer.Ordinal);
        List<InventoryStock> visible = [];
        foreach (InventoryRow row in data.Rows)
        {
            InventoryStock inventory = FromRows(row, reservationsByIngredient[row.IngredientId]);
            if (data.Tags.TryGetValue(inventory.EntityUid, out TagCollection? loadedTags))
            {
                inventory = inventory with { Tags = loadedTags };
            }
            InventoryFilter view = ToFilter(inventory);
            if (expression is not null && !expression.Match(view))
            {
                continue;
            }

            try
            {
                await AuthorizeAsync(context, InventoryAuthorization.List, inventory).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                AppError.IsPermission(exception) && !AppError.IsCancellation(exception))
            {
                continue;
            }

            visible.Add(inventory);
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
        return new Page<InventoryStock>(visible, next);
    }

    private static async Task<InventoryReservationRow[]> ReservationsAsync(
        OperationContext context,
        IngredientId ingredientId) =>
        await context.Session!.Context.Set<InventoryReservationRow>()
            .AsNoTracking()
            .Where(row => row.IngredientId == ingredientId.Value)
            .ToArrayAsync(context.CancellationToken)
            .ConfigureAwait(false);

    private ValueTask AuthorizeAsync(
        OperationContext context,
        EntityUid action,
        InventoryStock inventory) =>
        authorizer.AuthorizeAsync(
            context.Principal,
            action,
            inventory.ToCedarEntity(),
            context.CancellationToken);

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
            throw AppError.Internal("read inventory", exception);
        }
    }

    private static InventoryStock FromRows(
        InventoryRow row,
        IEnumerable<InventoryReservationRow> reservations)
    {
        try
        {
            Amount onHand = PersistedAmount(row.Quantity, row.Unit, $"inventory {row.Id}");
            return new InventoryStock(
                InventoryId.Parse(row.Id),
                IngredientId.Parse(row.IngredientId),
                onHand,
                ReservedAmount(onHand.Unit, reservations),
                PersistedPrice(row),
                new DateTimeOffset(DateTime.SpecifyKind(row.LastUpdatedUtc, DateTimeKind.Utc)),
                TagCollection.Empty).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted inventory {row.Id}", exception);
        }
    }

    private static Amount ReservedAmount(
        Unit stockUnit,
        IEnumerable<InventoryReservationRow> reservations)
    {
        Amount total = Amount.Create(0d, stockUnit);
        foreach (InventoryReservationRow reservation in reservations)
        {
            Amount amount = PersistedAmount(
                reservation.Quantity,
                reservation.Unit,
                $"inventory reservation {reservation.Id}");
            total = total.Add(amount.Convert(stockUnit));
        }

        return total;
    }

    private static Amount PersistedAmount(double quantity, string unit, string subject)
    {
        try
        {
            return Amount.Create(quantity, Unit.Parse(unit));
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted amount for {subject}", exception);
        }
    }

    private static Price? PersistedPrice(InventoryRow row)
    {
        if (row.UnitCostAmount is null && row.UnitCostCurrency is null)
        {
            return null;
        }

        if (row.UnitCostAmount is null || row.UnitCostCurrency is null)
        {
            throw AppError.Internal($"incomplete persisted unit cost for inventory {row.Id}");
        }

        try
        {
            return new Price(row.UnitCostAmount.Value, Currency.Parse(row.UnitCostCurrency));
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted unit cost for inventory {row.Id}", exception);
        }
    }

    private static InventoryRow ToRow(InventoryStock inventory) => new()
    {
        Id = inventory.Id.Value,
        IngredientId = inventory.IngredientId.Value,
        Quantity = inventory.OnHand.Value,
        Unit = inventory.OnHand.Unit.Value,
        UnitCostAmount = inventory.UnitCost?.Amount,
        UnitCostCurrency = inventory.UnitCost?.Currency.Code,
        LastUpdatedUtc = inventory.LastUpdated.UtcDateTime,
    };

    private static void CopyToRow(InventoryStock inventory, InventoryRow row)
    {
        row.Quantity = inventory.OnHand.Value;
        row.Unit = inventory.OnHand.Unit.Value;
        row.UnitCostAmount = inventory.UnitCost?.Amount;
        row.UnitCostCurrency = inventory.UnitCost?.Currency.Code;
        row.LastUpdatedUtc = inventory.LastUpdated.UtcDateTime;
    }

    private static InventoryFilter ToFilter(InventoryStock inventory) => new(
        inventory.Id.Value,
        inventory.IngredientId.Value,
        inventory.OnHand.Value,
        inventory.OnHand.Unit.Value,
        inventory.LastUpdated.UtcDateTime,
        inventory.Tags.Strings().ToArray());

    private static void RecordAdjustment(
        OperationContext context,
        InventoryStock inventory,
        string reason)
    {
        context.SelectResource(inventory.EntityUid);
        context.Touch(inventory.EntityUid);
        context.AddEvent(new StockAdjusted(
            inventory,
            reason,
            inventory.OnHand.Value < inventory.Reserved.Value));
    }

    private static void RequireIngredientId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }

    private static Operation Command(EntityUid action) => Operation.Command(ActionName(action));
    private static Operation Query(EntityUid action) => Operation.Query(ActionName(action));
    private static string ActionName(EntityUid action) => $"{action.Type}::\"{action.Id}\"";
}
