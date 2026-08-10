using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Tagging.Authorization;

public sealed class TaggingCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Tagging/Authorization/schema.cedarschema";
    public string SchemaText => Schema;
    public IReadOnlyCollection<string> ResourceTypes => [TaggingAuthorization.ResourceType];
    public IReadOnlyList<CedarPolicyDocument> Policies =>
    [
        new("Mixology.Modules.Tagging/Authorization/policies.cedar", Policy),
    ];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];

            entity TagDiscovery {
                Key: String,
                Value: String,
                Exact: Bool
            };
        }

        namespace Mixology::TagDiscovery {
            action show, summary appliesTo {
                principal: Mixology::Actor,
                resource: Mixology::TagDiscovery,
                context: {}
            };
        }
        """;

    private const string Policy = """
        // Discovery intentionally discloses matching entity types, names, and
        // IDs without consulting each owning domain's read policy.
        permit(
            principal == Mixology::Actor::"owner",
            action in [
                Mixology::TagDiscovery::Action::"show",
                Mixology::TagDiscovery::Action::"summary"
            ],
            resource is Mixology::TagDiscovery
        );
        """;
}
