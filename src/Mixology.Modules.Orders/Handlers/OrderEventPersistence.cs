using Microsoft.EntityFrameworkCore;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Persistence;

namespace Mixology.Modules.Orders.Handlers;

internal static class OrderEventPersistence
{
    public static async Task<OrderRow[]> LoadActiveByIngredientAsync(
        EventHandlerContext context,
        IngredientId ingredientId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ingredientId.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(ingredientId.Value);
        try
        {
            string value = ingredientId.Value;
            return await context.Session.Context.Set<OrderRow>()
                .Include(static row => row.IngredientUsage)
                .Include(static row => row.BlockedIngredients)
                .Where(row => row.DeletedAtUtc == null
                    && (row.Status == "pending" || row.Status == "blocked")
                    && row.IngredientUsage.Any(usage => usage.IngredientId == value))
                .OrderBy(static row => row.Id)
                .ToArrayAsync(context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal($"list active orders using ingredient {ingredientId}", exception);
        }
    }

    public static void ApplyBlockedIngredients(
        OrderRow row,
        IEnumerable<IngredientId> blockedIngredientIds)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(blockedIngredientIds);
        IngredientId[] blocked = blockedIngredientIds
            .Distinct()
            .OrderBy(static id => id.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (IngredientId id in blocked)
        {
            _ = IngredientId.Parse(id.Value);
        }

        HashSet<string> desired = blocked.Select(static id => id.Value)
            .ToHashSet(StringComparer.Ordinal);
        row.BlockedIngredients.RemoveAll(existing => !desired.Contains(existing.IngredientId));
        HashSet<string> retained = row.BlockedIngredients.Select(static value => value.IngredientId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (IngredientId ingredientId in blocked)
        {
            if (retained.Add(ingredientId.Value))
            {
                row.BlockedIngredients.Add(new OrderBlockedIngredientRow
                {
                    OrderId = row.Id,
                    IngredientId = ingredientId.Value,
                });
            }
        }

        row.BlockedIngredients.Sort(static (left, right) =>
            string.CompareOrdinal(left.IngredientId, right.IngredientId));
        row.Status = blocked.Length == 0 ? OrderStatus.Pending.Value : OrderStatus.Blocked.Value;
    }

    public static IReadOnlyList<IngredientId> BlockedIngredientIds(OrderRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            return row.BlockedIngredients
                .Select(static value => IngredientId.Parse(value.IngredientId))
                .ToArray();
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted blocked ingredients for order {row.Id}", exception);
        }
    }

    public static EntityUid EntityUid(OrderRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            return OrderId.Parse(row.Id).EntityUid;
        }
        catch (InvalidError exception)
        {
            throw AppError.Internal($"invalid persisted order id {row.Id}", exception);
        }
    }
}
