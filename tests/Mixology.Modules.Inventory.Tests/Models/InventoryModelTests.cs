using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Xunit;

namespace Mixology.Modules.Inventory.Tests.Models;

public sealed class InventoryModelTests
{
    [Fact]
    public void AvailableSubtractsConvertedReservationsAndClampsShortages()
    {
        InventoryStock available = Stock(Amount.Create(100d, Unit.Milliliter), Amount.Create(1d, Unit.Centiliter));
        InventoryStock shortage = Stock(Amount.Create(5d, Unit.Milliliter), Amount.Create(1d, Unit.Centiliter));

        Assert.Equal(90d, available.Available.Value, 6);
        Assert.Equal(Unit.Milliliter, available.Available.Unit);
        Assert.Equal(0d, shortage.Available.Value);
    }

    [Theory]
    [InlineData("received")]
    [InlineData("used")]
    [InlineData("spilled")]
    [InlineData("expired")]
    [InlineData("corrected")]
    public void AdjustmentReasonsRoundTripTheGoVocabulary(string value)
    {
        Assert.Equal(value, AdjustmentReason.Parse(value).Value);
    }

    [Fact]
    public void AdjustmentRequiresAChangeAndARecognizedReason()
    {
        IngredientId ingredientId = IngredientId.New();

        Assert.Throws<InvalidError>(() => new AdjustInventoryRequest(
            ingredientId,
            AdjustmentReason.Received).Validate());
        Assert.Throws<InvalidError>(() => AdjustmentReason.Parse("lost"));
    }

    private static InventoryStock Stock(Amount onHand, Amount reserved) => new(
        InventoryId.New(),
        IngredientId.New(),
        onHand,
        reserved,
        null,
        DateTimeOffset.UtcNow,
        TagCollection.Empty);
}
