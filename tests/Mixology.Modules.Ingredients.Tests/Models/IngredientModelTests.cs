using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Quality;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Xunit;

namespace Mixology.Modules.Ingredients.Tests.Models;

public sealed class IngredientModelTests
{
    public static TheoryData<string, IngredientCategory> Categories => new()
    {
        { "spirit", IngredientCategory.Spirit },
        { "mixer", IngredientCategory.Mixer },
        { "garnish", IngredientCategory.Garnish },
        { "bitter", IngredientCategory.Bitter },
        { "syrup", IngredientCategory.Syrup },
        { "juice", IngredientCategory.Juice },
        { "other", IngredientCategory.Other },
    };

    [Theory]
    [MemberData(nameof(Categories))]
    public void CategoryParsesEveryStableValue(string source, IngredientCategory expected)
    {
        Assert.Equal(expected, IngredientCategory.Parse($"  {source}  "));
        Assert.Equal(source, expected.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("SPIRIT")]
    public void CategoryRejectsMissingOrUnknownValues(string? source)
    {
        Assert.Throws<InvalidError>(() => IngredientCategory.Parse(source));
        Assert.False(IngredientCategory.TryParse(source, out _));
    }

    [Fact]
    public void CategoryOrderMatchesThePublicSurface()
    {
        Assert.Equal(
            ["spirit", "mixer", "garnish", "bitter", "syrup", "juice", "other"],
            IngredientCategory.All.Select(value => value.Value));
    }

    [Fact]
    public void IngredientNormalizationTrimsTextAndUsesUtcRetirementTime()
    {
        DateTimeOffset local = new(2026, 8, 9, 12, 0, 0, TimeSpan.FromHours(-7));
        Ingredient ingredient = Ingredient("  Gin  ", "  Juniper  ") with { DeletedAt = local };

        Ingredient normalized = ingredient.Normalize();

        Assert.Equal("Gin", normalized.Name);
        Assert.Equal("Juniper", normalized.Description);
        Assert.Equal(TimeSpan.Zero, normalized.DeletedAt?.Offset);
    }

    [Fact]
    public void RequestValidationUsesConcreteInvalidErrors()
    {
        Assert.Throws<InvalidError>(() => new CreateIngredientRequest(
            " ", IngredientCategory.Spirit, Unit.Ounce).Validate());
        Assert.Throws<InvalidError>(() => new UpdateIngredientRequest(default).Validate());
        Assert.Throws<InvalidError>(() => new ListIngredientsRequest(Limit: -1).Validate());
        Assert.Throws<InvalidError>(() => new ListIngredientsRequest(Cursor: "not-an-id").Validate());
    }

    [Fact]
    public void RetirementNormalizesDefaultRatioAndRejectsInvalidShapes()
    {
        IngredientId source = IngredientId.New();
        IngredientId replacement = IngredientId.New();

        RetireIngredientRequest normalized = new RetireIngredientRequest(
            source,
            new Retirement(replacement)).Normalize();

        Assert.Equal(1, normalized.Retirement.Ratio);
        Assert.Throws<InvalidError>(() => new Retirement(null, 1).Validate());
        Assert.Throws<InvalidError>(() => new Retirement(replacement, double.NaN).Validate());
        Assert.Throws<InvalidError>(() => new RetireIngredientRequest(source, new Retirement(source)).Validate());
    }

    [Fact]
    public void CatalogResolvesSymbolicNamesToLiveTypedIds()
    {
        Ingredient lime = Ingredient("  Lime   Juice  ");
        Ingredient lemon = Ingredient("Lemon Juice");

        SubstitutionRule rule = Assert.Single(IngredientSubstitutionCatalog.Resolve(lime.Id, [lime, lemon]));

        Assert.Equal(lime.Id, rule.IngredientId);
        Assert.Equal(lemon.Id, rule.SubstituteId);
        Assert.Equal(1, rule.Ratio);
        Assert.Equal(Quality.Similar, rule.QualityImpact);
        rule.Validate();
    }

    [Fact]
    public void CatalogOmitsMissingAndRetiredSubstitutes()
    {
        Ingredient lime = Ingredient("Lime Juice");
        Ingredient lemon = Ingredient("Lemon Juice") with { DeletedAt = DateTimeOffset.UtcNow };

        Assert.Empty(IngredientSubstitutionCatalog.Resolve(lime.Id, [lime]));
        Assert.Empty(IngredientSubstitutionCatalog.Resolve(lime.Id, [lime, lemon]));
    }

    private static Ingredient Ingredient(string name, string description = "") => new(
        IngredientId.New(),
        name,
        IngredientCategory.Spirit,
        Unit.Ounce,
        description,
        null,
        TagCollection.Empty);
}
