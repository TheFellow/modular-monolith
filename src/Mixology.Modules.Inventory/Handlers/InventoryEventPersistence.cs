using Microsoft.EntityFrameworkCore;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Inventory.Persistence;

namespace Mixology.Modules.Inventory.Handlers;

internal static class InventoryEventPersistence
{
    public static async Task<InventoryRow?> FindStockAsync(
        EventHandlerContext context,
        IngredientId ingredientId)
    {
        try
        {
            return await context.Session.Context.Set<InventoryRow>()
                .SingleOrDefaultAsync(
                    row => row.IngredientId == ingredientId.Value,
                    context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal($"read stock for ingredient {ingredientId}", exception);
        }
    }

    public static async Task<InventoryRow[]> LoadStocksAsync(
        EventHandlerContext context,
        IReadOnlyCollection<string> ingredientIds)
    {
        if (ingredientIds.Count == 0)
        {
            return [];
        }

        string[] ids = ingredientIds.Distinct(StringComparer.Ordinal).ToArray();
        try
        {
            return await context.Session.Context.Set<InventoryRow>()
                .Where(row => ids.Contains(row.IngredientId))
                .OrderBy(static row => row.IngredientId)
                .ToArrayAsync(context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read inventory stock for reservations", exception);
        }
    }

    public static async Task<InventoryReservationRow[]> LoadReservationsForIngredientsAsync(
        EventHandlerContext context,
        IReadOnlyCollection<string> ingredientIds)
    {
        if (ingredientIds.Count == 0)
        {
            return [];
        }

        string[] ids = ingredientIds.Distinct(StringComparer.Ordinal).ToArray();
        try
        {
            return await context.Session.Context.Set<InventoryReservationRow>()
                .Where(row => ids.Contains(row.IngredientId))
                .OrderBy(static row => row.IngredientId)
                .ThenBy(static row => row.OrderId)
                .ToArrayAsync(context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read inventory reservations", exception);
        }
    }

    public static async Task<InventoryReservationRow[]> LoadReservationsForOrderAsync(
        EventHandlerContext context,
        OrderId orderId)
    {
        try
        {
            return await context.Session.Context.Set<InventoryReservationRow>()
                .Where(row => row.OrderId == orderId.Value)
                .OrderBy(static row => row.IngredientId)
                .ThenBy(static row => row.Id)
                .ToArrayAsync(context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal($"list reservations for order {orderId}", exception);
        }
    }

    public static Amount StockAmount(InventoryRow row)
    {
        try
        {
            return Amount.Create(row.Quantity, Unit.Parse(row.Unit));
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted amount for inventory {row.Id}", exception);
        }
    }

    public static Amount ReservationAmount(InventoryReservationRow row)
    {
        try
        {
            return Amount.Create(row.Quantity, Unit.Parse(row.Unit));
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted amount for inventory reservation {row.Id}", exception);
        }
    }

    public static EntityUid EntityUid(InventoryRow row)
    {
        try
        {
            return InventoryId.Parse(row.Id).EntityUid;
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted inventory id {row.Id}", exception);
        }
    }
}
