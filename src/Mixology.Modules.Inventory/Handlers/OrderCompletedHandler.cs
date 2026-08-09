using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Modules.Orders.Events;

namespace Mixology.Modules.Inventory.Handlers;

public sealed class OrderCompletedHandler(TimeProvider timeProvider)
    : IDomainEventHandler<OrderCompleted>
{
    public async Task HandleAsync(EventHandlerContext context, OrderCompleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        InventoryReservationRow[] reservations =
            await InventoryEventPersistence.LoadReservationsForOrderAsync(
                context,
                domainEvent.Order.Id).ConfigureAwait(false);
        if (reservations.Length == 0)
        {
            return;
        }

        string[] ingredientIds = reservations.Select(static row => row.IngredientId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        InventoryRow[] loadedStocks = await InventoryEventPersistence.LoadStocksAsync(
            context,
            ingredientIds).ConfigureAwait(false);
        Dictionary<string, InventoryRow> stocks = loadedStocks.ToDictionary(
            static row => row.IngredientId,
            StringComparer.Ordinal);
        List<ConsumptionPlan> plans = new(ingredientIds.Length);
        foreach (IGrouping<string, InventoryReservationRow> group in reservations.GroupBy(
            static row => row.IngredientId,
            StringComparer.Ordinal))
        {
            if (!stocks.TryGetValue(group.Key, out InventoryRow? stock))
            {
                throw AppError.NotFound($"stock for ingredient {group.Key} not found");
            }

            Amount current = InventoryEventPersistence.StockAmount(stock);
            Amount consumed = Amount.Create(0d, current.Unit);
            foreach (InventoryReservationRow reservation in group)
            {
                consumed = consumed.Add(
                    InventoryEventPersistence.ReservationAmount(reservation).Convert(current.Unit));
            }

            Amount remaining = current.Subtract(consumed);
            if (remaining.Value < 0d)
            {
                remaining = Amount.Create(0d, current.Unit);
            }

            plans.Add(new ConsumptionPlan(stock, remaining));
        }

        DateTime updatedAt = timeProvider.GetUtcNow().UtcDateTime;
        foreach (ConsumptionPlan plan in plans)
        {
            plan.Stock.Quantity = plan.Remaining.Value;
            plan.Stock.Unit = plan.Remaining.Unit.Value;
            plan.Stock.LastUpdatedUtc = updatedAt;
            context.Touch(InventoryEventPersistence.EntityUid(plan.Stock));
        }

        context.Session.Context.RemoveRange(reservations);
    }

    private sealed record ConsumptionPlan(InventoryRow Stock, Amount Remaining);
}
