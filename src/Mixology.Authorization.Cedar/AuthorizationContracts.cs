using Cedar.Types;
using Mixology.Application.Authentication;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Authorization.Cedar;

public sealed record CedarPolicyDocument(string Name, string Text);

public interface ICedarAuthorizationModule
{
    string SchemaName { get; }
    string SchemaText { get; }
    IReadOnlyCollection<string> ResourceTypes { get; }
    IReadOnlyList<CedarPolicyDocument> Policies { get; }
}

public interface IEntityAuthorizer
{
    ValueTask AuthorizeAsync(
        Actor principal,
        KernelEntityUid action,
        Entity resource,
        CancellationToken cancellationToken = default);
}
