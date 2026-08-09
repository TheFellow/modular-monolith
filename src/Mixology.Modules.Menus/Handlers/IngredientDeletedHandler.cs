using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;

namespace Mixology.Modules.Menus.Handlers;

public sealed class IngredientDeletedHandler(DrinkQueries drinks, IMenuOperations operations)
    : IPreparingDomainEventHandler<IngredientDeleted>, IFinalizingDomainEventHandler<IngredientDeleted>
{
    private PreparedMenu[] prepared = [];

    public async Task PrepareAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        prepared = [];
        IReadOnlyList<Drink> affected = await drinks.ListByIngredientAsync(
            context.Session,
            domainEvent.Ingredient.Id,
            context.CancellationToken).ConfigureAwait(false);
        HashSet<string> drinkIds = affected.Select(static drink => drink.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        MenuRow[] menus = await MenuEventPersistence.LoadByDrinkIdsAsync(
            context,
            drinkIds,
            tracking: false).ConfigureAwait(false);
        prepared = menus.Select(menu => new PreparedMenu(
            menu.Id,
            MenuId.Parse(menu.Id).EntityUid,
            menu.Items.Where(item => drinkIds.Contains(item.DrinkId))
                .Select(static item => item.DrinkId)
                .ToHashSet(StringComparer.Ordinal))).ToArray();
    }

    public Task HandleAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Task.CompletedTask;
    }

    public async Task FinalizeAsync(EventHandlerContext context, IngredientDeleted domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (prepared.Length == 0)
        {
            return;
        }

        MenuRow[] loaded = await MenuEventPersistence.LoadByIdsAsync(
            context,
            prepared.Select(static plan => plan.Id).ToArray()).ConfigureAwait(false);
        Dictionary<string, MenuRow> rows = loaded.ToDictionary(static row => row.Id, StringComparer.Ordinal);
        foreach (PreparedMenu plan in prepared)
        {
            if (!rows.TryGetValue(plan.Id, out MenuRow? menu))
            {
                throw AppError.Internal($"recalculate prepared menu {plan.Id}: row is missing");
            }

            if (!await MenuAvailabilityReaction.RecalculateAsync(
                context,
                menu,
                operations,
                plan.DrinkIds).ConfigureAwait(false))
            {
                continue;
            }

            context.Touch(plan.EntityUid);
        }
    }

    private sealed record PreparedMenu(
        string Id,
        EntityUid EntityUid,
        IReadOnlySet<string> DrinkIds);
}
