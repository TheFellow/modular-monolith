using System.Text.Json;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Money;
using Xunit;

namespace Mixology.Kernel.Tests.Money;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("USD", "USD")]
    [InlineData("EUR", "EUR")]
    public void CurrencyParsesKnownCodes(string source, string expected)
    {
        Assert.Equal(expected, Currency.Parse(source).Code);
    }

    [Fact]
    public void CurrencyRejectsUnknownCode()
    {
        InvalidError error = Assert.Throws<InvalidError>(() => Currency.Parse("unknown"));
        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }

    [Fact]
    public void CurrencyFormatsAndRoundTripsJson()
    {
        Assert.Equal("$12.50", Currency.Usd.Format("12.50"));
        Assert.Equal("12.50 €", Currency.Eur.Format("12.50"));
        Assert.Equal("\"USD\"", JsonSerializer.Serialize(Currency.Usd));
        Assert.Equal(Currency.Eur, JsonSerializer.Deserialize<Currency>("\"EUR\""));
    }

    [Theory]
    [InlineData("$1.23", "$1.23")]
    [InlineData("$ 1.23", "$1.23")]
    [InlineData("USD 1.23", "$1.23")]
    [InlineData("1.23 usd", "$1.23")]
    [InlineData("EUR 1.23", "1.23 €")]
    [InlineData("1.23 EUR", "1.23 €")]
    public void PriceParsesUserFacingForms(string source, string expected)
    {
        Assert.Equal(expected, Price.Parse(source).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.23")]
    [InlineData("US 1.23")]
    [InlineData("USD nope")]
    [InlineData("XYZ 1.23")]
    public void PriceRejectsMalformedValues(string source)
    {
        Assert.Throws<InvalidError>(() => Price.Parse(source));
    }

    [Theory]
    [InlineData("1.025", 103)]
    [InlineData("1.024", 102)]
    public void CentsRoundHalfUp(string source, int expected)
    {
        Price price = new(decimal.Parse(source, System.Globalization.CultureInfo.InvariantCulture), Currency.Usd);

        Assert.Equal(expected, price.Cents());
    }

    [Fact]
    public void ArithmeticPreservesCurrencyAndRejectsMismatch()
    {
        Price oneDollar = Price.FromCents(100, Currency.Usd);

        Assert.Equal(250, oneDollar.Multiply(2.5m).Cents());
        Assert.Equal(200, oneDollar.Add(oneDollar).Cents());
        Assert.Throws<InvalidError>(() => oneDollar.Add(Price.FromCents(100, Currency.Eur)));
    }

    [Fact]
    public void SuggestedPriceCeilsToCent()
    {
        Assert.Equal(334, Price.FromCents(100, Currency.Usd).SuggestedPrice(0.70d).Cents());
    }
}
