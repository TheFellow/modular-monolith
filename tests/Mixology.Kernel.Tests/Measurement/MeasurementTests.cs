using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Xunit;

namespace Mixology.Kernel.Tests.Measurement;

public sealed class MeasurementTests
{
    [Fact]
    public void VolumeConversionsPreserveReferenceFactors()
    {
        Assert.Equal(29.5735d, Volume.FromOunces(1d).Milliliters, 4);
        Assert.Equal(1d, Volume.FromMilliliters(29.5735d).Ounces, 4);
        Assert.Equal(100d, Volume.FromCentiliters(10d).Milliliters, 4);
    }

    [Fact]
    public void QuantityConvertsWithoutLosingCanonicalVolume()
    {
        Quantity ounces = new(1.5d, Unit.Ounce);
        Quantity milliliters = ounces.Convert(Unit.Milliliter);

        Assert.Equal(44.36025d, milliliters.Value, 4);
        Assert.Equal(Unit.Milliliter, milliliters.Unit);
    }

    [Fact]
    public void AmountIsAClosedVolumeOrDiscreteUnion()
    {
        Assert.IsType<VolumeAmount>(Amount.Create(1d, Unit.Ounce));
        Assert.IsType<DiscreteAmount>(Amount.Create(1d, Unit.Dash));
    }

    [Fact]
    public void VolumeAmountsAddAcrossConvertibleUnits()
    {
        Amount ounce = Amount.Create(1d, Unit.Ounce);
        Amount milliliters = Amount.Create(29.5735d, Unit.Milliliter);

        Amount sum = ounce.Add(milliliters);

        Assert.Equal(2d, sum.Value, 4);
        Assert.Equal(Unit.Ounce, sum.Unit);
    }

    [Fact]
    public void DiscreteAmountsRejectMismatchedUnits()
    {
        Amount dash = Amount.Create(1d, Unit.Dash);
        Amount piece = Amount.Create(1d, Unit.Piece);

        AppError error = Assert.Throws<AppError>(() => dash.Add(piece));
        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }

    [Theory]
    [InlineData("oz")]
    [InlineData("ml")]
    [InlineData("cl")]
    [InlineData("dash")]
    [InlineData("piece")]
    [InlineData("splash")]
    public void UnitParsesEverySupportedValue(string source)
    {
        Assert.Equal(source, Unit.Parse(source).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public void UnitRejectsInvalidValue(string source)
    {
        Assert.Throws<AppError>(() => Unit.Parse(source));
    }
}

