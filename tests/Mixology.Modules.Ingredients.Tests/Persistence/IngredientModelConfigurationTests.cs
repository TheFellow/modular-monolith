using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mixology.Modules.Ingredients.Persistence;
using Mixology.Persistence;
using Xunit;

namespace Mixology.Modules.Ingredients.Tests.Persistence;

public sealed class IngredientModelConfigurationTests
{
    [Fact]
    public void ConfigurationDefinesThePrivateIngredientSchema()
    {
        DbContextOptions<MixologyDbContext> options = new DbContextOptionsBuilder<MixologyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using MixologyDbContext context = new(options, [new IngredientModelConfiguration()]);
        IEntityType entity = Assert.Single(context.Model.GetEntityTypes());

        Assert.Equal("Mixology.Modules.Ingredients.Persistence.IngredientRow", entity.Name);
        Assert.Equal("ingredients", entity.GetTableName());
        Assert.Equal("Id", Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        AssertColumn(entity, "Id", "id", nullable: false);
        AssertColumn(entity, "Name", "name", nullable: false);
        AssertColumn(entity, "Category", "category", nullable: false);
        AssertColumn(entity, "Unit", "unit", nullable: false);
        AssertColumn(entity, "Description", "description", nullable: false);
        AssertColumn(entity, "DeletedAtUtc", "deleted_at_utc", nullable: true);

        IIndex nameIndex = Assert.Single(entity.GetIndexes(), index => index.Properties.Single().Name == "Name");
        Assert.True(nameIndex.IsUnique);
        IIndex categoryIndex = Assert.Single(
            entity.GetIndexes(),
            index => index.Properties.Single().Name == "Category");
        Assert.False(categoryIndex.IsUnique);
    }

    private static void AssertColumn(IEntityType entity, string propertyName, string columnName, bool nullable)
    {
        IProperty property = entity.FindProperty(propertyName)!;
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(nullable, property.IsNullable);
    }
}
