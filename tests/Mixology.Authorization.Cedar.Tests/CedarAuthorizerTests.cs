using Cedar.Types;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Errors;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Authorization.Cedar.Tests;

public sealed class CedarAuthorizerTests
{
    private static readonly KernelEntityUid Read = new("Mixology::Thing::Action", "read");
    private static readonly Entity Resource = new(
        new EntityUid(new EntityType("Mixology::Thing"), new CedarString("thing-1")),
        new EntityUidSet(),
        new CedarRecord(),
        new CedarRecord());

    [Fact]
    public async Task PolicyPermitAndDefaultDenyBecomePreciseApplicationOutcomes()
    {
        CedarAuthorizer authorizer = new([new TestModule()]);

        await authorizer.AuthorizeAsync(Actor.Owner, Read, Resource);
        await Assert.ThrowsAsync<PermissionError>(async () =>
            await authorizer.AuthorizeAsync(Actor.Anonymous, Read, Resource));
    }

    [Fact]
    public async Task UnknownResourceAndActionFailAsInternalConfigurationErrors()
    {
        CedarAuthorizer authorizer = new([new TestModule()]);
        Entity unknown = Resource with
        {
            Uid = new EntityUid(new EntityType("Mixology::Unknown"), new CedarString("one")),
        };

        await Assert.ThrowsAsync<InternalError>(async () =>
            await authorizer.AuthorizeAsync(Actor.Owner, Read, unknown));
        await Assert.ThrowsAsync<InternalError>(async () =>
            await authorizer.AuthorizeAsync(
                Actor.Owner,
                new KernelEntityUid("Mixology::Thing::Action", "missing"),
                Resource));
    }

    private sealed class TestModule : ICedarAuthorizationModule
    {
        public string SchemaName => "test.cedarschema";

        public string SchemaText => """
            namespace Mixology {
                entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];
                entity Thing;
            }

            namespace Mixology::Thing {
                action read appliesTo {
                    principal: Mixology::Actor,
                    resource: Mixology::Thing,
                    context: {}
                };
            }
            """;

        public IReadOnlyCollection<string> ResourceTypes => ["Mixology::Thing"];

        public IReadOnlyList<CedarPolicyDocument> Policies =>
        [
            new("test.cedar", """
                permit(
                    principal == Mixology::Actor::"owner",
                    action == Mixology::Thing::Action::"read",
                    resource
                );
                """),
        ];
    }
}
