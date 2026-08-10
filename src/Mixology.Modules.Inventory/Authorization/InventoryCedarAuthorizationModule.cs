using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Inventory.Authorization;

public sealed class InventoryCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Inventory/Authorization/schema.cedarschema";

    public string SchemaText => Schema;

    public IReadOnlyCollection<string> ResourceTypes => [InventoryAuthorization.ResourceType];

    public IReadOnlyList<CedarPolicyDocument> Policies =>
    [
        new("Mixology.Modules.Inventory/Authorization/policies.cedar", Policy),
    ];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];
            entity Ingredient;

            entity Inventory {
                IngredientID: Ingredient,
                Unit: String
            } tags String;
        }

        namespace Mixology::Inventory {
            action list, get, adjust, set, tag, untag appliesTo {
                principal: Mixology::Actor,
                resource: Mixology::Inventory,
                context: {}
            };
        }
        """;

    private const string Policy = """
        permit(
            principal,
            action in [
                Mixology::Inventory::Action::"list",
                Mixology::Inventory::Action::"get"
            ],
            resource
        );

        permit(
            principal == Mixology::Actor::"manager",
            action in [
                Mixology::Inventory::Action::"adjust",
                Mixology::Inventory::Action::"set",
                Mixology::Inventory::Action::"tag",
                Mixology::Inventory::Action::"untag"
            ],
            resource is Mixology::Inventory
        );
        """;
}
