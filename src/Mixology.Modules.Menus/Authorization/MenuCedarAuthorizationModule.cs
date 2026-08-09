using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Menus.Authorization;

public sealed class MenuCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Menus/Authorization/schema.cedarschema";
    public string SchemaText => Schema;
    public IReadOnlyCollection<string> ResourceTypes => [MenuAuthorization.ResourceType];
    public IReadOnlyList<CedarPolicyDocument> Policies =>
    [
        new("Mixology.Modules.Menus/Authorization/policies.cedar", Policy),
    ];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];

            entity Menu {
                Name: String,
                Status: String
            } tags String;
        }

        namespace Mixology::Menu {
            action list, get, readiness, create, update, delete, add_drink,
                   remove_drink, publish, draft, tag, untag appliesTo {
                principal: Mixology::Actor,
                resource: Mixology::Menu,
                context: {}
            };
        }
        """;

    private const string Policy = """
        // Menus are customer-facing, so list and get are public.
        permit(
            principal,
            action in [
                Mixology::Menu::Action::"list",
                Mixology::Menu::Action::"get"
            ],
            resource
        );

        // Managers operate menu composition and lifecycle. Owners are covered
        // by the application's global owner policy.
        permit(
            principal == Mixology::Actor::"manager",
            action in [
                Mixology::Menu::Action::"readiness",
                Mixology::Menu::Action::"create",
                Mixology::Menu::Action::"update",
                Mixology::Menu::Action::"delete",
                Mixology::Menu::Action::"add_drink",
                Mixology::Menu::Action::"remove_drink",
                Mixology::Menu::Action::"publish",
                Mixology::Menu::Action::"draft",
                Mixology::Menu::Action::"tag",
                Mixology::Menu::Action::"untag"
            ],
            resource is Mixology::Menu
        );
        """;
}
