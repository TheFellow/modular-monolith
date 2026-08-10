using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Authorization;
using Mixology.Modules.Drinks.Models;
using Xunit;

namespace Mixology.Modules.Drinks.Tests;

public sealed class DrinkAuthorizationTests
{
    public static TheoryData<Actor, DrinkCategory, EntityUid, bool> Matrix => new()
    {
        { Actor.Owner, DrinkCategory.Wine, DrinkAuthorization.Create, true },
        { Actor.Manager, DrinkCategory.Wine, DrinkAuthorization.Create, true },
        { Actor.Manager, DrinkCategory.Cocktail, DrinkAuthorization.Delete, true },
        { Actor.Sommelier, DrinkCategory.Wine, DrinkAuthorization.Update, true },
        { Actor.Sommelier, DrinkCategory.Cocktail, DrinkAuthorization.Update, false },
        { Actor.Bartender, DrinkCategory.Cocktail, DrinkAuthorization.Update, true },
        { Actor.Bartender, DrinkCategory.Wine, DrinkAuthorization.Update, false },
        { Actor.Anonymous, DrinkCategory.Wine, DrinkAuthorization.Get, true },
        { Actor.Anonymous, DrinkCategory.Cocktail, DrinkAuthorization.List, true },
        { Actor.Anonymous, DrinkCategory.Cocktail, DrinkAuthorization.Create, false },
        { Actor.Sommelier, DrinkCategory.Wine, DrinkAuthorization.Get, true },
        { Actor.Sommelier, DrinkCategory.Cocktail, DrinkAuthorization.Get, false },
        { Actor.Bartender, DrinkCategory.Cocktail, DrinkAuthorization.Get, true },
        { Actor.Bartender, DrinkCategory.Wine, DrinkAuthorization.Get, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task PoliciesPreserveCategoryBasedAccess(
        Actor actor,
        DrinkCategory category,
        EntityUid action,
        bool allowed)
    {
        ServiceCollection services = new();
        services.AddDrinksModule();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IEntityAuthorizer authorizer = provider.GetRequiredService<IEntityAuthorizer>();
        Drink drink = Drink(category);

        if (allowed)
        {
            await authorizer.AuthorizeAsync(actor, action, drink.ToCedarEntity());
        }
        else
        {
            await Assert.ThrowsAsync<PermissionError>(async () =>
                await authorizer.AuthorizeAsync(actor, action, drink.ToCedarEntity()));
        }
    }

    [Fact]
    public async Task SommelierAudienceTagExtendsReadAccessWithoutChangingCategoryRules()
    {
        ServiceCollection services = new();
        services.AddDrinksModule();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IEntityAuthorizer authorizer = provider.GetRequiredService<IEntityAuthorizer>();
        Drink tagged = Drink(DrinkCategory.Cocktail) with
        {
            Tags = TagCollection.FromDictionary(new Dictionary<string, string>
            {
                ["audience"] = "sommelier",
            }),
        };

        await authorizer.AuthorizeAsync(Actor.Sommelier, DrinkAuthorization.Get, tagged.ToCedarEntity());
        await Assert.ThrowsAsync<PermissionError>(async () =>
            await authorizer.AuthorizeAsync(Actor.Sommelier, DrinkAuthorization.Update, tagged.ToCedarEntity()));
    }

    private static Drink Drink(DrinkCategory category) => new(
        DrinkId.New(),
        "Policy drink",
        category,
        GlassType.Coupe,
        new Recipe(
            [new RecipeIngredient(IngredientId.New(), Amount.Create(1, Unit.Ounce))],
            ["Serve"]),
        string.Empty,
        DrinkStatus.Active,
        null,
        TagCollection.Empty);
}
