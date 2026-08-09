using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Kernel.Tests.Entities;

public sealed class EntityIdTests
{
    public static TheoryData<IEntityId, string, string> GeneratedIds => new()
    {
        { DrinkId.New(), "drk", EntityIds.DrinkType },
        { IngredientId.New(), "ing", EntityIds.IngredientType },
        { InventoryId.New(), "inv", EntityIds.InventoryType },
        { MenuId.New(), "mnu", EntityIds.MenuType },
        { OrderId.New(), "ord", EntityIds.OrderType },
        { AuditEntryId.New(), "aud", EntityIds.AuditEntryType },
    };

    [Theory]
    [MemberData(nameof(GeneratedIds))]
    public void GeneratedIdsAreCanonicalAndInferTheirEntityType(IEntityId id, string prefix, string entityType)
    {
        EntityUid inferred = EntityIds.Parse(id.Value);

        Assert.StartsWith($"{prefix}-", id.Value, StringComparison.Ordinal);
        Assert.Equal(31, id.Value.Length);
        Assert.False(id.IsEmpty);
        Assert.Equal(entityType, inferred.Type);
        Assert.Equal(id.Value, inferred.Id);
    }

    [Fact]
    public void TypedParserRejectsWrongPrefix()
    {
        string ingredient = IngredientId.New().Value;

        InvalidError error = Assert.Throws<InvalidError>(() => DrinkId.Parse(ingredient));
        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-separator")]
    [InlineData("wat-3BxsD9vQRgeYqJ8v4bFVvytN")]
    [InlineData("drk-not-a-ksuid")]
    [InlineData("drk-!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData("drk-zzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void PrefixInferenceRejectsInvalidIds(string value)
    {
        InvalidError error = Assert.Throws<InvalidError>(() => EntityIds.Parse(value));

        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }

    [Fact]
    public void GoGeneratedKsuidParses()
    {
        const string value = "drk-3BxsD9vQRgeYqJ8v4bFVvytN1JU";

        DrinkId id = DrinkId.Parse(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(EntityIds.DrinkType, id.EntityUid.Type);
    }
}
