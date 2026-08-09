using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Ingredients.Authorization;

public sealed class IngredientCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Ingredients/Authorization/schema.cedarschema";

    public string SchemaText => Schema;

    public IReadOnlyCollection<string> ResourceTypes => [IngredientAuthorization.ResourceType];

    public IReadOnlyList<CedarPolicyDocument> Policies =>
    [
        new("Mixology.Modules.Ingredients/Authorization/policies.cedar", Policy),
    ];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];

            entity Ingredient {
                Name: String,
                Category: String,
                Unit: String
            } tags String;
        }

        namespace Mixology::Ingredient {
            action list, get, create, update, retire, tag, untag appliesTo {
                principal: Mixology::Actor,
                resource: Mixology::Ingredient,
                context: {}
            };
        }
        """;

    private const string Policy = """
        // Anyone can read ingredients.
        permit(
            principal,
            action in [
                Mixology::Ingredient::Action::"list",
                Mixology::Ingredient::Action::"get"
            ],
            resource
        );

        // Managers can modify ingredients.
        permit(
            principal == Mixology::Actor::"manager",
            action in [
                Mixology::Ingredient::Action::"create",
                Mixology::Ingredient::Action::"update",
                Mixology::Ingredient::Action::"retire",
                Mixology::Ingredient::Action::"tag",
                Mixology::Ingredient::Action::"untag"
            ],
            resource is Mixology::Ingredient
        );
        """;
}
