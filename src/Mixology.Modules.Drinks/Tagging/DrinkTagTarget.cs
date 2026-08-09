using Mixology.Kernel.Entities;
using Mixology.Modules.Drinks.Authorization;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Modules.Drinks.Tagging;

internal sealed class DrinkTagTarget(DrinkQueries queries) : ITagTargetRegistrationProvider
{
    public TagTargetRegistration Registration { get; } = new(
        EntityIds.DrinkType,
        DrinkAuthorization.Get,
        DrinkAuthorization.Tag,
        DrinkAuthorization.Untag,
        async (session, raw, cancellationToken) =>
        {
            Drink value = await queries.GetAsync(
                session,
                DrinkId.Parse(raw),
                cancellationToken).ConfigureAwait(false);
            return new TagTargetState(value.ToCedarEntity(), value.Name);
        },
        async (session, ids, cancellationToken) =>
            await queries.ActiveIdsAsync(session, ids, cancellationToken).ConfigureAwait(false));
}
