using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Events;
using Mixology.Modules.Menus.Persistence;

namespace Mixology.Modules.Menus.Handlers;

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
        string[] ids = affected.Select(static drink => drink.Id.Value).ToArray();
        MenuRow[] menus = await MenuEventPersistence.LoadByDrinkIdsAsync(context, ids).ConfigureAwait(false);
        foreach (MenuRow menu in menus)
        {
            context.Touch(Mixology.Kernel.Entities.MenuId.Parse(menu.Id).EntityUid);
        }
    }
}
