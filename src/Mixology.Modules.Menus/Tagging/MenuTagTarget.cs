using Mixology.Kernel.Entities;
using Mixology.Modules.Menus.Authorization;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Queries;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Modules.Menus.Tagging;

internal sealed class MenuTagTarget(MenuQueries queries) : ITagTargetRegistrationProvider
{
    public TagTargetRegistration Registration { get; } = new(
        EntityIds.MenuType,
        MenuAuthorization.Get,
        MenuAuthorization.Tag,
        MenuAuthorization.Untag,
        async (session, raw, cancellationToken) =>
        {
            Menu value = await queries.GetAsync(
                session,
                MenuId.Parse(raw),
                cancellationToken).ConfigureAwait(false);
            return new TagTargetState(value.ToCedarEntity(), value.Name);
        },
        async (session, ids, cancellationToken) =>
            await queries.ActiveIdsAsync(session, ids, cancellationToken).ConfigureAwait(false));
}
