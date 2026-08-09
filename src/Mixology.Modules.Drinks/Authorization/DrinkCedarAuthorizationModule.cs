using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Drinks.Authorization;

public sealed class DrinkCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Drinks/Authorization/schema.cedarschema";
    public string SchemaText => Schema;
    public IReadOnlyCollection<string> ResourceTypes => [DrinkAuthorization.ResourceType];
    public IReadOnlyList<CedarPolicyDocument> Policies =>
        [new("Mixology.Modules.Drinks/Authorization/policies.cedar", Policy)];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];
            entity Drink {
                Name: String,
                Category: String,
                Glass: String,
                Description: String
            } tags String;
        }

        namespace Mixology::Drink {
            action list, get, create, update, delete, tag, untag appliesTo {
                principal: Mixology::Actor,
                resource: Mixology::Drink,
                context: {}
            };
        }
        """;

    private const string Policy = """
        permit(principal == Mixology::Actor::"manager", action in [Mixology::Drink::Action::"list", Mixology::Drink::Action::"get"], resource is Mixology::Drink);
        permit(principal == Mixology::Actor::"anonymous", action in [Mixology::Drink::Action::"list", Mixology::Drink::Action::"get"], resource is Mixology::Drink);
        permit(principal == Mixology::Actor::"sommelier", action in [Mixology::Drink::Action::"list", Mixology::Drink::Action::"get"], resource is Mixology::Drink) when { resource.Category == "wine" };
        permit(principal == Mixology::Actor::"sommelier", action in [Mixology::Drink::Action::"list", Mixology::Drink::Action::"get"], resource is Mixology::Drink) when { resource.hasTag("audience") && resource.getTag("audience") == "sommelier" };
        permit(principal == Mixology::Actor::"bartender", action in [Mixology::Drink::Action::"list", Mixology::Drink::Action::"get"], resource is Mixology::Drink) when { resource.Category != "wine" };
        permit(principal == Mixology::Actor::"manager", action in [Mixology::Drink::Action::"create", Mixology::Drink::Action::"update", Mixology::Drink::Action::"delete", Mixology::Drink::Action::"tag", Mixology::Drink::Action::"untag"], resource is Mixology::Drink);
        permit(principal == Mixology::Actor::"sommelier", action in [Mixology::Drink::Action::"create", Mixology::Drink::Action::"update", Mixology::Drink::Action::"delete", Mixology::Drink::Action::"tag", Mixology::Drink::Action::"untag"], resource is Mixology::Drink) when { resource.Category == "wine" };
        permit(principal == Mixology::Actor::"bartender", action in [Mixology::Drink::Action::"create", Mixology::Drink::Action::"update", Mixology::Drink::Action::"delete", Mixology::Drink::Action::"tag", Mixology::Drink::Action::"untag"], resource is Mixology::Drink) when { resource.Category != "wine" };
        """;
}
