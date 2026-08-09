using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Drinks.Models;
using Xunit;

namespace Mixology.Modules.Drinks.Tests;

public sealed class DrinkModelTests
{
    [Fact]
    public void CategoryAndGlassValuesHaveStablePublicOrder()
    {
        Assert.Equal(
            ["cocktail", "mocktail", "wine", "shot", "highball", "martini", "sour", "tiki"],
            DrinkCategory.All.Select(static value => value.Value));
        Assert.Equal(
            ["rocks", "highball", "coupe", "martini"],
            GlassType.All.Select(static value => value.Value));
        Assert.Equal(DrinkCategory.Wine, DrinkCategory.Parse(" wine "));
        Assert.Equal(GlassType.Coupe, GlassType.Parse(" coupe "));
        Assert.Equal(default, DrinkCategory.Parse(""));
        Assert.Equal(default, GlassType.Parse(""));
    }

    [Theory]
    [InlineData("beer")]
    [InlineData("WINE")]
    public void UnknownCategoryIsPreciselyInvalid(string value)
    {
        Assert.Throws<InvalidError>(() => DrinkCategory.Parse(value));
    }

    [Fact]
    public void StatusIsClosedAndRejectsAnEmptyDefault()
    {
        Assert.Equal(DrinkStatus.Active, DrinkStatus.Parse("active"));
        Assert.Equal(DrinkStatus.ReviewRequired, DrinkStatus.Parse("review_required"));
        Assert.Throws<InvalidError>(() => default(DrinkStatus).Validate());
    }

    [Fact]
    public void RecipeNormalizationCopiesAndTrimsTheCompleteRecipe()
    {
        IngredientId ingredient = IngredientId.New();
        IngredientId substitute = IngredientId.New();
        Recipe recipe = new(
            [new RecipeIngredient(ingredient, Amount.Create(1, Unit.Ounce), substitutes: [substitute])],
            ["  Shake  "],
            "  Lemon twist  ");

        Recipe normalized = recipe.Normalize();

        Assert.Equal("Shake", Assert.Single(normalized.Steps));
        Assert.Equal("Lemon twist", normalized.Garnish);
        Assert.Equal(substitute, Assert.Single(Assert.Single(normalized.Ingredients).Substitutes));
    }

    [Fact]
    public void RecipeValidationReportsItsExactStructuralLocation()
    {
        InvalidError noIngredients = Assert.Throws<InvalidError>(() => new Recipe([], ["Shake"]).Validate());
        Assert.Equal("recipe must have at least 1 ingredient", noIngredients.Message);
        InvalidError noSteps = Assert.Throws<InvalidError>(() => new Recipe(
            [new RecipeIngredient(IngredientId.New(), Amount.Create(1, Unit.Ounce))], []).Validate());
        Assert.Equal("recipe must have at least 1 step", noSteps.Message);
        InvalidError blankStep = Assert.Throws<InvalidError>(() => new Recipe(
            [new RecipeIngredient(IngredientId.New(), Amount.Create(1, Unit.Ounce))], [" "]).Validate());
        Assert.Equal("recipe step 0: cannot be blank", blankStep.Message);
        InvalidError zero = Assert.Throws<InvalidError>(() => new Recipe(
            [new RecipeIngredient(IngredientId.New(), Amount.Create(0, Unit.Ounce))], ["Shake"]).Validate());
        Assert.Equal("recipe ingredient 0: amount must be > 0", zero.Message);
    }
}
