using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Events;

namespace Mixology.Modules.Drinks.Handlers;

public sealed class IngredientUpdatedHandler(DrinkQueries drinks)
    : IDomainEventHandler<IngredientUpdated>
{
    public async Task HandleAsync(EventHandlerContext context, IngredientUpdated domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);
        IReadOnlyList<Drink> affected = await drinks.ListByIngredientAsync(
            context.Session,
            domainEvent.Ingredient.Id,
            context.CancellationToken).ConfigureAwait(false);
        foreach (Drink drink in affected)
        {
            context.Touch(drink.EntityUid);
        }
    }
}
