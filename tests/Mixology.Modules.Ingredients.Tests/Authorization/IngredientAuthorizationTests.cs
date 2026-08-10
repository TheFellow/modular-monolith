using Cedar.Types;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Models;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Ingredients.Tests.Authorization;

public sealed class IngredientAuthorizationTests
{
    private static readonly IngredientAuthorizationResource Resource = new(
        new KernelEntityUid("Wrong::Type", "ing-test"),
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["audience"] = "members",
            ["featured"] = string.Empty,
        },
        "spirit",
        "Gin",
        "ml");

    public static TheoryData<Actor, KernelEntityUid, bool> Matrix => new()
    {
        { Actor.Owner, IngredientAuthorization.List, true },
        { Actor.Owner, IngredientAuthorization.Get, true },
        { Actor.Owner, IngredientAuthorization.Create, true },
        { Actor.Owner, IngredientAuthorization.Update, true },
        { Actor.Owner, IngredientAuthorization.Retire, true },
        { Actor.Owner, IngredientAuthorization.Tag, true },
        { Actor.Owner, IngredientAuthorization.Untag, true },
        { Actor.Manager, IngredientAuthorization.List, true },
        { Actor.Manager, IngredientAuthorization.Get, true },
        { Actor.Manager, IngredientAuthorization.Create, true },
        { Actor.Manager, IngredientAuthorization.Update, true },
        { Actor.Manager, IngredientAuthorization.Retire, true },
        { Actor.Manager, IngredientAuthorization.Tag, true },
        { Actor.Manager, IngredientAuthorization.Untag, true },
        { Actor.Sommelier, IngredientAuthorization.List, true },
        { Actor.Sommelier, IngredientAuthorization.Get, true },
        { Actor.Sommelier, IngredientAuthorization.Create, false },
        { Actor.Sommelier, IngredientAuthorization.Update, false },
        { Actor.Sommelier, IngredientAuthorization.Retire, false },
        { Actor.Sommelier, IngredientAuthorization.Tag, false },
        { Actor.Sommelier, IngredientAuthorization.Untag, false },
        { Actor.Bartender, IngredientAuthorization.List, true },
        { Actor.Bartender, IngredientAuthorization.Get, true },
        { Actor.Bartender, IngredientAuthorization.Create, false },
        { Actor.Bartender, IngredientAuthorization.Update, false },
        { Actor.Bartender, IngredientAuthorization.Retire, false },
        { Actor.Bartender, IngredientAuthorization.Tag, false },
        { Actor.Bartender, IngredientAuthorization.Untag, false },
        { Actor.Anonymous, IngredientAuthorization.List, true },
        { Actor.Anonymous, IngredientAuthorization.Get, true },
        { Actor.Anonymous, IngredientAuthorization.Create, false },
        { Actor.Anonymous, IngredientAuthorization.Update, false },
        { Actor.Anonymous, IngredientAuthorization.Retire, false },
        { Actor.Anonymous, IngredientAuthorization.Tag, false },
        { Actor.Anonymous, IngredientAuthorization.Untag, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task PoliciesPreserveTheActorActionMatrix(
        Actor actor,
        KernelEntityUid action,
        bool allowed)
    {
        ServiceCollection services = new();
        services.AddCedarAuthorization();
        services.AddIngredientsModule();
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
    public void ResourceConversionUsesTheSchemaShapeAndNormalizesTheType()
    {
        Entity entity = Resource.ToCedarEntity();

        Assert.Equal(IngredientAuthorization.ResourceType, entity.Uid.Type.Value);
        Assert.Equal("ing-test", entity.Uid.Id.Value);
        Assert.Empty(entity.Parents);
        Assert.Equal("spirit", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Category")]).Value);
        Assert.Equal("Gin", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Name")]).Value);
        Assert.Equal("ml", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Unit")]).Value);
        Assert.Equal("members", Assert.IsType<CedarString>(entity.Tags[new CedarString("audience")]).Value);
        Assert.Equal(string.Empty, Assert.IsType<CedarString>(entity.Tags[new CedarString("featured")]).Value);
    }

    [Fact]
    public void DomainIngredientConvertsWithoutLeakingCedarIntoTheModel()
    {
        Ingredient ingredient = new(
            IngredientId.New(),
            "Gin",
            IngredientCategory.Spirit,
            Unit.Milliliter,
            "London dry gin",
            null,
            TagCollection.FromDictionary(new Dictionary<string, string>
            {
                ["audience"] = "members",
            }));

        Entity entity = ingredient.ToCedarEntity();

        Assert.Equal(ingredient.Id.Value, entity.Uid.Id.Value);
        Assert.Equal("spirit", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Category")]).Value);
        Assert.Equal("ml", Assert.IsType<CedarString>(entity.Attributes[new CedarString("Unit")]).Value);
        Assert.Equal("members", Assert.IsType<CedarString>(entity.Tags[new CedarString("audience")]).Value);
    }
}
