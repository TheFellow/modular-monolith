using Mixology.Kernel.Entities;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Inventory.Authorization;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Queries;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Modules.Inventory.Tagging;

internal sealed class InventoryTagTarget(
    InventoryQueries inventory,
    IngredientQueries ingredients) : ITagTargetRegistrationProvider
{
    public TagTargetRegistration Registration { get; } = new(
        EntityIds.InventoryType,
        InventoryAuthorization.Get,
        InventoryAuthorization.Tag,
        InventoryAuthorization.Untag,
        async (session, raw, cancellationToken) =>
        {
            InventoryStock value = await inventory.GetByIdAsync(
                session,
                InventoryId.Parse(raw),
                cancellationToken).ConfigureAwait(false);
            Ingredient ingredient = await ingredients.GetAsync(
                session,
                value.IngredientId,
                cancellationToken).ConfigureAwait(false);
            return new TagTargetState(value.ToCedarEntity(), $"Inventory for {ingredient.Name}");
        },
        async (session, ids, cancellationToken) =>
            await inventory.ActiveIdsAsync(session, ids, cancellationToken).ConfigureAwait(false));
}
