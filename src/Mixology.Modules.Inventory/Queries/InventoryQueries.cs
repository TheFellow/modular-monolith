using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Tags;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Inventory.Queries;

/// <summary>
/// Owner-defined inventory reads for collaborating domains that already own a store session.
/// </summary>
public sealed class InventoryQueries(ITagReader tags)
{
    public async Task<InventoryStock> GetAsync(
        StoreSession session,
        IngredientId ingredientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireId(ingredientId);
        try
        {
            InventoryRow? row = await session.Context.Set<InventoryRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.IngredientId == ingredientId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                throw AppError.NotFound($"stock for ingredient {ingredientId} not found");
            }

            InventoryReservationRow[] reservations = await session.Context.Set<InventoryReservationRow>()
                .AsNoTracking()
                .Where(candidate => candidate.IngredientId == ingredientId.Value)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            InventoryStock inventory = FromRows(row, reservations);
            return inventory with
            {
                Tags = await tags.ListAsync(
                    session.Context,
                    inventory.EntityUid,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read inventory", exception);
        }
    }

    public async Task<InventoryStock> GetByIdAsync(
        StoreSession session,
        InventoryId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _ = InventoryId.Parse(id.Value);
        try
        {
            InventoryRow? row = await session.Context.Set<InventoryRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == id.Value, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                throw AppError.NotFound($"inventory {id} not found");
            }

            InventoryReservationRow[] reservations = await session.Context.Set<InventoryReservationRow>()
                .AsNoTracking()
                .Where(candidate => candidate.IngredientId == row.IngredientId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            InventoryStock inventory = FromRows(row, reservations);
            return inventory with
            {
                Tags = await tags.ListAsync(
                    session.Context,
                    inventory.EntityUid,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read inventory", exception);
        }
    }

    public async Task<IReadOnlySet<string>> ActiveIdsAsync(
        StoreSession session,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ids);
        InventoryId[] requested = ids.Distinct(StringComparer.Ordinal).Select(InventoryId.Parse).ToArray();
        string[] values = requested.Select(static value => value.Value).ToArray();
        try
        {
            string[] active = await session.Context.Set<InventoryRow>()
                .AsNoTracking()
                .Where(row => values.Contains(row.Id))
                .Select(static row => row.Id)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return active.ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read active inventory ids", exception);
        }
    }

    private static InventoryStock FromRows(
        InventoryRow row,
        IEnumerable<InventoryReservationRow> reservations)
    {
        try
        {
            Amount onHand = Amount.Create(row.Quantity, Unit.Parse(row.Unit));
            Amount reserved = Amount.Create(0d, onHand.Unit);
            foreach (InventoryReservationRow reservation in reservations)
            {
                reserved = reserved.Add(
                    Amount.Create(reservation.Quantity, Unit.Parse(reservation.Unit)).Convert(onHand.Unit));
            }

            return new InventoryStock(
                InventoryId.Parse(row.Id),
                IngredientId.Parse(row.IngredientId),
                onHand,
                reserved,
                Price(row),
                new DateTimeOffset(DateTime.SpecifyKind(row.LastUpdatedUtc, DateTimeKind.Utc)),
                TagCollection.Empty,
                row.Revision).Normalize();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted inventory {row.Id}", exception);
        }
    }

    private static Price? Price(InventoryRow row)
    {
        if (row.UnitCostAmount is null && row.UnitCostCurrency is null)
        {
            return null;
        }

        if (row.UnitCostAmount is not { } amount || row.UnitCostCurrency is not { } currency)
        {
            throw AppError.Internal($"incomplete persisted unit cost for inventory {row.Id}");
        }

        return new Price(amount, Currency.Parse(currency));
    }

    private static void RequireId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }
}
