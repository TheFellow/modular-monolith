using System.Globalization;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Modules.Orders.Events;
using Mixology.Modules.Orders.Models;

namespace Mixology.Modules.Inventory.Handlers;

public sealed class OrderPlacedHandler : IDomainEventHandler<OrderPlaced>
{
    public async Task HandleAsync(EventHandlerContext context, OrderPlaced domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        Order order = domainEvent.Order.Normalize();
        if (order.IngredientUsage.Count == 0)
        {
            return;
        }

        string[] ingredientIds = order.IngredientUsage
            .Select(static usage => usage.IngredientId.Value)
            .ToArray();
        if (ingredientIds.Distinct(StringComparer.Ordinal).Count() != ingredientIds.Length)
        {
            throw AppError.Conflict($"order {order.Id} contains duplicate ingredient usage");
        }

        InventoryRow[] loadedStocks = await InventoryEventPersistence.LoadStocksAsync(
            context,
            ingredientIds).ConfigureAwait(false);
        Dictionary<string, InventoryRow> stocks = loadedStocks.ToDictionary(
            static row => row.IngredientId,
            StringComparer.Ordinal);
        InventoryReservationRow[] existing =
            await InventoryEventPersistence.LoadReservationsForIngredientsAsync(
                context,
                ingredientIds).ConfigureAwait(false);
        ILookup<string, InventoryReservationRow> reservedByIngredient = existing.ToLookup(
            static row => row.IngredientId,
            StringComparer.Ordinal);
        HashSet<string> reservationIds = existing.Select(static row => row.Id)
            .ToHashSet(StringComparer.Ordinal);
        List<ReservationPlan> plans = new(order.IngredientUsage.Count);
        foreach (IngredientUsage usage in order.IngredientUsage)
        {
            if (!stocks.TryGetValue(usage.IngredientId.Value, out InventoryRow? stock))
            {
                throw AppError.NotFound($"stock for ingredient {usage.IngredientId} not found");
            }

            Amount onHand = InventoryEventPersistence.StockAmount(stock);
            Amount requested = usage.Amount.Convert(onHand.Unit);
            Amount reserved = Amount.Create(0d, onHand.Unit);
            foreach (InventoryReservationRow row in reservedByIngredient[usage.IngredientId.Value])
            {
                reserved = reserved.Add(
                    InventoryEventPersistence.ReservationAmount(row).Convert(onHand.Unit));
            }

            double available = onHand.Value - reserved.Value;
            if (available < requested.Value)
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"insufficient available stock for ingredient {usage.IngredientId}: " +
                    $"need {requested.Value:g} {requested.Unit}, available {available:g} {onHand.Unit}");
                throw AppError.FailedPrecondition(detail);
            }

            string reservationId = $"{order.Id.Value}:{usage.IngredientId.Value}";
            if (reservationIds.Contains(reservationId))
            {
                throw AppError.Conflict($"stock is already reserved for order {order.Id}");
            }

            plans.Add(new ReservationPlan(stock, usage, requested, reservationId));
        }

        foreach (ReservationPlan plan in plans)
        {
            context.Session.Context.Add(new InventoryReservationRow
            {
                Id = plan.Id,
                OrderId = order.Id.Value,
                IngredientId = plan.Usage.IngredientId.Value,
                Quantity = plan.Amount.Value,
                Unit = plan.Amount.Unit.Value,
            });
            context.Touch(InventoryEventPersistence.EntityUid(plan.Stock));
        }
    }

    private sealed record ReservationPlan(
        InventoryRow Stock,
        IngredientUsage Usage,
        Amount Amount,
        string Id);
}
