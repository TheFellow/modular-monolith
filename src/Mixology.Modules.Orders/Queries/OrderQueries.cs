using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Persistence;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;

namespace Mixology.Modules.Orders.Queries;

/// <summary>Owner-defined order reads for collaborating domains inside an existing session.</summary>
public sealed class OrderQueries(ITagReader tags)
{
    public async Task<Order> GetAsync(
        StoreSession session,
        OrderId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _ = OrderId.Parse(id.Value);
        try
        {
            OrderRow? row = await OrdersModule.Rows(session.Context)
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == id.Value && candidate.DeletedAtUtc == null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                throw AppError.NotFound($"order {id} not found");
            }

            Order order = OrdersModule.FromRow(row);
            return order with
            {
                Tags = await tags.ListAsync(
                    session.Context,
                    order.EntityUid,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read order", exception);
        }
    }

    public async Task<IReadOnlySet<string>> ActiveIdsAsync(
        StoreSession session,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ids);
        OrderId[] requested = ids.Distinct(StringComparer.Ordinal).Select(OrderId.Parse).ToArray();
        string[] values = requested.Select(static value => value.Value).ToArray();
        try
        {
            string[] active = await session.Context.Set<OrderRow>()
                .AsNoTracking()
                .Where(row => values.Contains(row.Id) && row.DeletedAtUtc == null)
                .Select(static row => row.Id)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return active.ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("read active order ids", exception);
        }
    }
}
