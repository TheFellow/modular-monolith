using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Inventory.Authorization;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Inventory.Tests.Authorization;

public sealed class InventoryAuthorizationTests
{
    private static readonly InventoryAuthorizationResource Resource = new(
        new KernelEntityUid("Wrong::Type", "inv-test"),
        IngredientId.New(),
        "ml",
        new Dictionary<string, string>());

    public static TheoryData<Actor, KernelEntityUid, bool> Matrix => new()
    {
        { Actor.Anonymous, InventoryAuthorization.List, true },
        { Actor.Bartender, InventoryAuthorization.Get, true },
        { Actor.Manager, InventoryAuthorization.Adjust, true },
        { Actor.Manager, InventoryAuthorization.Set, true },
        { Actor.Owner, InventoryAuthorization.Adjust, true },
        { Actor.Owner, InventoryAuthorization.Set, true },
        { Actor.Sommelier, InventoryAuthorization.Adjust, false },
        { Actor.Bartender, InventoryAuthorization.Set, false },
        { Actor.Anonymous, InventoryAuthorization.Tag, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task PoliciesPreserveTheInventoryPermissionMatrix(
        Actor actor,
        KernelEntityUid action,
        bool allowed)
    {
        ServiceCollection services = new();
        services.AddInventoryModule();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IEntityAuthorizer authorizer = provider.GetRequiredService<IEntityAuthorizer>();

        if (allowed)
        {
            await authorizer.AuthorizeAsync(actor, action, Resource.ToCedarEntity());
            return;
        }

        await Assert.ThrowsAsync<PermissionError>(async () =>
            await authorizer.AuthorizeAsync(actor, action, Resource.ToCedarEntity()));
    }
}
