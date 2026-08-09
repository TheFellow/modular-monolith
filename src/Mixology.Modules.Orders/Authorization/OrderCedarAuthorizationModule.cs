using Mixology.Authorization.Cedar;

namespace Mixology.Modules.Orders.Authorization;

public sealed class OrderCedarAuthorizationModule : ICedarAuthorizationModule
{
    public string SchemaName => "Mixology.Modules.Orders/Authorization/schema.cedarschema";
    public string SchemaText => Schema;
    public IReadOnlyCollection<string> ResourceTypes => [OrderAuthorization.ResourceType];
    public IReadOnlyList<CedarPolicyDocument> Policies => [new("Mixology.Modules.Orders/Authorization/policies.cedar", Policy)];

    private const string Schema = """
        namespace Mixology {
            entity Actor enum ["owner", "manager", "sommelier", "bartender", "anonymous"];
            entity Menu;
            entity Order { MenuID: Menu, Status: String } tags String;
        }
        namespace Mixology::Order {
            action list, get, place, complete, cancel, tag, untag appliesTo { principal: Mixology::Actor, resource: Mixology::Order, context: {} };
        }
        """;

    private const string Policy = """
        permit(principal == Mixology::Actor::"manager", action in [Mixology::Order::Action::"list", Mixology::Order::Action::"get"], resource);
        permit(principal == Mixology::Actor::"sommelier", action in [Mixology::Order::Action::"list", Mixology::Order::Action::"get"], resource);
        permit(principal == Mixology::Actor::"bartender", action in [Mixology::Order::Action::"list", Mixology::Order::Action::"get"], resource);
        permit(principal == Mixology::Actor::"manager", action in [Mixology::Order::Action::"place", Mixology::Order::Action::"complete", Mixology::Order::Action::"cancel", Mixology::Order::Action::"tag", Mixology::Order::Action::"untag"], resource is Mixology::Order);
        permit(principal == Mixology::Actor::"bartender", action in [Mixology::Order::Action::"place", Mixology::Order::Action::"complete", Mixology::Order::Action::"cancel", Mixology::Order::Action::"tag", Mixology::Order::Action::"untag"], resource is Mixology::Order);
        """;
}
