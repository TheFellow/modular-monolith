using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Ingredients.Persistence;
using Mixology.Persistence;

namespace Mixology.Modules.Ingredients.Queries;

/// <summary>
/// Owner-defined queries available to collaborating domains inside an existing
/// store session. These queries deliberately do not re-enter the application
/// middleware or perform a second authorization decision.
/// </summary>
public sealed class IngredientQueries
{
    public async Task RequireActiveAsync(
        StoreSession session,
        IngredientId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (id.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(id.Value);
        try
        {
            bool exists = await session.Context.Set<IngredientRow>()
                .AnyAsync(
                    row => row.Id == id.Value && row.DeletedAtUtc == null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
            {
                throw AppError.NotFound($"ingredient {id} not found");
            }
        }
        catch (Exception exception) when (exception is not AppError and not OperationCanceledException)
        {
            throw AppError.Internal("read ingredient", exception);
        }
    }
}
