using Mixology.Kernel.Entities;
using Mixology.Kernel.Tags;
using Mixology.Persistence;

namespace Mixology.Modules.Tagging.Models;

public interface ITagReader
{
    Task<TagCollection> ListAsync(
        MixologyDbContext database,
        EntityUid target,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<EntityUid, TagCollection>> ListTypeAsync(
        MixologyDbContext database,
        string entityType,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default);
}

public interface ITagTargetRegistrationProvider
{
    TagTargetRegistration Registration { get; }
}

public delegate ValueTask<TagTargetState> LoadTagTarget(
    StoreSession session,
    string id,
    CancellationToken cancellationToken);

public delegate ValueTask<IReadOnlySet<string>> LoadActiveTagTargetIds(
    StoreSession session,
    IReadOnlyCollection<string> ids,
    CancellationToken cancellationToken);

/// <summary>
/// Domain-owned tag behavior. Operational domains publish only this contract;
/// the Tagging module remains independent of their models and persistence.
/// </summary>
public sealed record TagTargetRegistration(
    string EntityType,
    EntityUid GetAction,
    EntityUid TagAction,
    EntityUid UntagAction,
    LoadTagTarget LoadAsync,
    LoadActiveTagTargetIds ActiveIdsAsync);
