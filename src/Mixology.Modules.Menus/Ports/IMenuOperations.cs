using Mixology.Kernel.Entities;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Menus.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Menus.Ports;

public interface IMenuOperations
{
    ValueTask<MenuDrink> GetDrinkAsync(
        StoreSession session,
        DrinkId id,
        CancellationToken cancellationToken = default);
    ValueTask<Availability> GetAvailabilityAsync(
        StoreSession session,
        DrinkId id,
        CancellationToken cancellationToken = default);
    ValueTask<ReadinessReport> GetReadinessAsync(
        StoreSession session,
        Menu menu,
        CancellationToken cancellationToken = default);
    ValueTask<MenuAnalysis> AnalyzeAsync(
        StoreSession session,
        Menu menu,
        double targetMargin,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<IngredientFulfillment>?> FulfillIngredientsAsync(
        StoreSession session,
        IReadOnlyList<RecipeIngredient> requirements,
        CancellationToken cancellationToken = default);
}
