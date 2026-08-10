using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Audit.Authorization;

public sealed class AuditCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Audit/Authorization/schema.cedarschema";

    public string SchemaText => Schema;

    public IReadOnlyCollection<string> ResourceTypes => [AuditAuthorization.ResourceType];

    public IReadOnlyList<CedarPolicyDocument> Policies =>
    [
        new("Mixology.Modules.Audit/Authorization/policies.cedar", Policy),
    ];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];
            entity AuditEntry;
        }

        namespace Mixology::AuditEntry {
            action list, get appliesTo {
                principal: Mixology::Actor,
                resource: Mixology::AuditEntry,
                context: {}
            };
        }
        """;

    private const string Policy = """
        // Audit logs are owner-only. The global owner policy supplies the permit;
        // these explicit forbids preserve the closed actor matrix.
        forbid(
            principal == Mixology::Actor::"manager",
            action in [
                Mixology::AuditEntry::Action::"list",
                Mixology::AuditEntry::Action::"get"
            ],
            resource
        );

        forbid(
            principal == Mixology::Actor::"sommelier",
            action in [
                Mixology::AuditEntry::Action::"list",
                Mixology::AuditEntry::Action::"get"
            ],
            resource
        );

        forbid(
            principal == Mixology::Actor::"bartender",
            action in [
                Mixology::AuditEntry::Action::"list",
                Mixology::AuditEntry::Action::"get"
            ],
            resource
        );

        forbid(
            principal == Mixology::Actor::"anonymous",
            action in [
                Mixology::AuditEntry::Action::"list",
                Mixology::AuditEntry::Action::"get"
            ],
            resource
        );
        """;
}
