using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Presentation.Mutations;

/// <summary>
/// Coordinates an owner-domain mutation and complete tag replacement without teaching either
/// module about the other. A null tag collection means "not specified"; an empty collection is
/// an explicit request to remove every tag.
/// </summary>
public sealed class TaggedMutationCoordinator(TaggingModule tagging)
{
    public async Task<TEntity> RunAsync<TEntity>(
        MixologySession session,
        Func<MixologySession, CancellationToken, Task<TEntity>> mutate,
        TagCollection? desiredTags,
        Func<TEntity, EntityUid> target,
        Func<TEntity, TagCollection, TEntity> withTags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mutate);

        try
        {
            if (desiredTags is null)
            {
                return await mutate(session, cancellationToken).ConfigureAwait(false);
            }

            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(withTags);
            desiredTags.Validate();
            return await session.ExecuteAtomicAsync(
                mutate,
                async (transactionSession, entity, token) =>
                {
                    TagMutationResult replacement = await tagging.ReplaceAsync(
                        transactionSession,
                        target(entity),
                        desiredTags,
                        token).ConfigureAwait(false);
                    return withTags(entity, replacement.Tags);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception) when (AppError.Find(exception) is not null)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppError.Internal("run tagged mutation", exception);
        }
    }
}
