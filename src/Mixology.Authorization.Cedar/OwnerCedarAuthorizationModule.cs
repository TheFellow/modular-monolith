namespace Mixology.Authorization.Cedar;

internal sealed class OwnerCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Authorization.Cedar/base.cedarschema";

    public string SchemaText => """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];

            action login appliesTo {
                principal: Actor,
                resource: Mixology::Auth::Session,
                context: {}
            };
        }

        namespace Mixology::Auth {
            entity Session;
        }
        """;

    public IReadOnlyCollection<string> ResourceTypes => [];

    public IReadOnlyList<CedarPolicyDocument> Policies =>
    [
        new("Mixology.Authorization.Cedar/base.cedar", """
            permit(
                principal == Mixology::Actor::"owner",
                action,
                resource
            );
            """),
    ];
}
