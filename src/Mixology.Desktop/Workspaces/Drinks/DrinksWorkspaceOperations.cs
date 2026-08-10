using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Presentation.Mutations;

namespace Mixology.Desktop.Workspaces.Drinks;

public interface IDrinksWorkspaceOperations
{
    Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken);

    Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActionState>> ProjectAsync(Drink? selected, CancellationToken cancellationToken);

    Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(CancellationToken cancellationToken);

    Task<Drink> CreateAsync(
        CreateDrinkRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken);

    Task<Drink> UpdateAsync(
        UpdateDrinkRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken);

    Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken);
}

internal sealed class ModuleDrinksWorkspaceOperations(
    DrinksModule drinks,
    IngredientsModule ingredients,
    DrinkActionProjector projector,
    TaggedMutationCoordinator taggedMutations,
    MixologySession session,
    Actor actor) : IDrinksWorkspaceOperations
{
    public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken) =>
        drinks.ListAsync(session, request, cancellationToken);

    public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
        drinks.GetAsync(session, id, cancellationToken);

    public Task<IReadOnlyList<ActionState>> ProjectAsync(
        Drink? selected,
        CancellationToken cancellationToken) => projector.ProjectAsync(actor, selected, cancellationToken);

    public async Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(
        CancellationToken cancellationToken)
    {
        List<Ingredient> result = [];
        Cursor cursor = default;
        do
        {
            Page<Ingredient> page = await ingredients.ListAsync(
                session,
                new ListIngredientsRequest(Cursor: cursor),
                cancellationToken).ConfigureAwait(false);
            result.AddRange(page.Items);
            cursor = page.Next;
        }
        while (!cursor.IsEmpty);

        return result;
    }

    public Task<Drink> CreateAsync(
        CreateDrinkRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken) => taggedMutations.RunAsync(
            session,
            (active, token) => drinks.CreateAsync(active, request, token),
            desiredTags,
            static value => value.EntityUid,
            static (value, tags) => value with { Tags = tags },
            cancellationToken);

    public Task<Drink> UpdateAsync(
        UpdateDrinkRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken) => taggedMutations.RunAsync(
            session,
            (active, token) => drinks.UpdateAsync(active, request, token),
            desiredTags,
            static value => value.EntityUid,
            static (value, tags) => value with { Tags = tags },
            cancellationToken);

    public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken) =>
        drinks.DeleteAsync(session, id, cancellationToken);
}
