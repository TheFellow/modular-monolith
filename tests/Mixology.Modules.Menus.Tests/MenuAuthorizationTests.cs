using Cedar.Types;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Authorization;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Menus.Tests;

public sealed class MenuAuthorizationTests
{
    private static readonly MenuAuthorizationResource Resource = new(
        new KernelEntityUid("Wrong::Type", "mnu-test"),
        new Dictionary<string, string>(StringComparer.Ordinal) { ["audience"] = "public" },
        "Dinner",
        "draft");

    public static TheoryData<Actor, KernelEntityUid, bool> Matrix => new()
    {
        { Actor.Owner, MenuAuthorization.Readiness, true },
        { Actor.Owner, MenuAuthorization.Publish, true },
        { Actor.Manager, MenuAuthorization.Readiness, true },
        { Actor.Manager, MenuAuthorization.Publish, true },
        { Actor.Bartender, MenuAuthorization.List, true },
        { Actor.Bartender, MenuAuthorization.Get, true },
        { Actor.Bartender, MenuAuthorization.Publish, false },
        { Actor.Sommelier, MenuAuthorization.List, true },
        { Actor.Sommelier, MenuAuthorization.Create, false },
        { Actor.Anonymous, MenuAuthorization.List, true },
        { Actor.Anonymous, MenuAuthorization.Get, true },
        { Actor.Anonymous, MenuAuthorization.Readiness, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task PoliciesPreserveThePublicReadManagerWriteMatrix(
        Actor actor,
        KernelEntityUid action,
        bool allowed)
    {
        ServiceCollection services = new();
        services.AddCedarAuthorization();
        services.AddMenusModule();
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

    [Fact]
    public void ResourceAdapterNormalizesTheTypeAndMapsSchemaAttributes()
    {
        Entity entity = Resource.ToCedarEntity();

        Assert.Equal(MenuAuthorization.ResourceType, entity.Uid.Type.Value);
        Assert.Equal("mnu-test", entity.Uid.Id.Value);
        Assert.Equal("Dinner", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Name")]).Value);
        Assert.Equal("draft", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Status")]).Value);
        Assert.Equal("public", Assert.IsType<CedarString>(entity.Tags[new CedarString("audience")]).Value);
    }
}
