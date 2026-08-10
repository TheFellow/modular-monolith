using Mixology.Kernel.Entities;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Queries;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Queries;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Modules.Orders.Tagging;

internal sealed class OrderTagTarget(
    OrderQueries orders,
    MenuQueries menus) : ITagTargetRegistrationProvider
{
    public TagTargetRegistration Registration { get; } = new(
        EntityIds.OrderType,
        OrderAuthorization.Get,
        OrderAuthorization.Tag,
        OrderAuthorization.Untag,
        async (session, raw, cancellationToken) =>
        {
            Order value = await orders.GetAsync(
                session,
                OrderId.Parse(raw),
                cancellationToken).ConfigureAwait(false);
            Menu menu = await menus.GetAsync(session, value.MenuId, cancellationToken).ConfigureAwait(false);
            return new TagTargetState(value.ToCedarEntity(), $"Order for {menu.Name}");
        },
        async (session, ids, cancellationToken) =>
            await orders.ActiveIdsAsync(session, ids, cancellationToken).ConfigureAwait(false));
}
