using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mixology.Modules.Drinks.Persistence;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Drinks.Tests;

public sealed class DrinkModelConfigurationTests
{
    [Fact]
    public void ConfigurationDefinesNormalizedRecipeStorageAndIndexes()
    {
        DbContextOptions<MixologyDbContext> options = new DbContextOptionsBuilder<MixologyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using MixologyDbContext context = new(options, [new DrinkModelConfiguration()]);
        Dictionary<string, IEntityType> entities = context.Model.GetEntityTypes().ToDictionary(
            static entity => entity.GetTableName()!,
            StringComparer.Ordinal);

        Assert.Equal(
            ["drink_recipe_ingredients", "drink_recipe_steps", "drink_recipe_substitutes", "drinks"],
            entities.Keys.Order(StringComparer.Ordinal));
        IEntityType drinks = entities["drinks"];
        Assert.True(Assert.Single(drinks.GetIndexes(), index =>
            index.Properties.Single().Name == "Name").IsUnique);
        Assert.Contains(drinks.GetIndexes(), index => index.Properties.Single().Name == "Category");
        Assert.Contains(drinks.GetIndexes(), index => index.Properties.Single().Name == "Glass");
        Assert.Contains(drinks.GetIndexes(), index => index.Properties.Single().Name == "Status");
        Assert.Equal(2, entities["drink_recipe_ingredients"].FindPrimaryKey()!.Properties.Count);
        Assert.Equal(3, entities["drink_recipe_substitutes"].FindPrimaryKey()!.Properties.Count);
        Assert.All(
            entities.Values.SelectMany(static entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior));
    }
}
