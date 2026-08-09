using Mixology.Kernel.Entities;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Modules.Ingredients.Tagging;

internal sealed class IngredientTagTarget(IngredientQueries queries) : ITagTargetRegistrationProvider
{
    public TagTargetRegistration Registration { get; } = new(
        EntityIds.IngredientType,
        IngredientAuthorization.Get,
        IngredientAuthorization.Tag,
        IngredientAuthorization.Untag,
        async (session, raw, cancellationToken) =>
        {
            Ingredient value = await queries.GetAsync(
                session,
                IngredientId.Parse(raw),
                cancellationToken).ConfigureAwait(false);
            return new TagTargetState(value.ToCedarEntity(), value.Name);
        },
        async (session, ids, cancellationToken) =>
            await queries.ActiveIdsAsync(session, ids, cancellationToken).ConfigureAwait(false));
}
