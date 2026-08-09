using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Filtering.Tests;

public sealed class FilterExpressionTests
{
    private static readonly FilterSchema<DrinkView> Schema = new(
        [
            Filter.Field("Name", (DrinkView view) => view.Name),
            Filter.Field("Category", (DrinkView view) => view.Category),
            Filter.Field("Price", (DrinkView view) => view.Price),
            Filter.Field("Active", (DrinkView view) => view.Active),
            Filter.Field("Created", (DrinkView view) => view.Created),
            Filter.Field("Age", (DrinkView view) => view.Age),
            Filter.Field("Tags", (DrinkView view) => view.Tags),
        ],
        "Category == \"wine\"",
        "Tags contains \"featured\"");

    private static readonly DrinkView Martini = new(
        "Martini",
        "cocktail",
        14,
        true,
        DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        TimeSpan.FromHours(2),
        ["featured", "classic"]);

    [Fact]
    public void EmptyInputMeansNoExpression()
    {
        Assert.Null(Filter.Parse(Schema, "  "));
    }

    [Theory]
    [InlineData("Active && Price >= 10", true)]
    [InlineData("Category == \"wine\" or Name == \"Martini\"", true)]
    [InlineData("Category in [\"wine\", \"beer\"]", false)]
    [InlineData("Category not in [\"wine\", \"beer\"]", true)]
    [InlineData("Name.contains(\"art\")", true)]
    [InlineData("Name startsWith \"Mar\"", true)]
    [InlineData("Name.endsWith(\"ini\")", true)]
    [InlineData("Name.matches(\"^M.*i$\")", true)]
    [InlineData("Tags contains \"featured\"", true)]
    [InlineData("!Active", false)]
    [InlineData("Created >= date(\"2026-08-01T00:00:00Z\")", true)]
    [InlineData("Age == duration(\"2h\")", true)]
    public void CheckedExpressionsEvaluateExactly(string source, bool expected)
    {
        FilterExpression<DrinkView> expression = Filter.Parse(Schema, source)!;

        Assert.Equal(expected, expression.Match(Martini));
    }

    [Theory]
    [InlineData("Category and Active")]
    [InlineData("Missing == 1")]
    [InlineData("Price == \"expensive\"")]
    [InlineData("Price + 1 > 2")]
    [InlineData("unknown(\"x\")")]
    [InlineData("Name.matches(Category)")]
    [InlineData("Name.matches(\"[\")")]
    [InlineData("Created > date(\"nope\")")]
    [InlineData("Age > duration(\"soon\")")]
    public void InvalidExpressionsFailBeforeEvaluation(string source)
    {
        AppError error = Assert.Throws<AppError>(() => Filter.Parse(Schema, source));

        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }

    [Theory]
    [InlineData("Active and Price >= 10 or Category == \"wine\"", "Active && Price >= 10 || Category == \"wine\"")]
    [InlineData("not (Category == \"wine\" or Active)", "!(Category == \"wine\" || Active)")]
    [InlineData("Name contains \"art\"", "Name.contains(\"art\")")]
    [InlineData("Category in [\"wine\",\"beer\"]", "Category in [\"wine\", \"beer\"]")]
    public void CanonicalTextIsStable(string source, string expected)
    {
        FilterExpression<DrinkView> expression = Filter.Parse(Schema, source)!;

        Assert.Equal(expected, expression.Canonical);
        Assert.Equal(expected, Filter.Parse(Schema, expected)!.Canonical);
    }

    [Fact]
    public void TreeIsApplicationOwnedAndTyped()
    {
        FilterExpression<DrinkView> expression = Filter.Parse(Schema, "Price >= 10 && Active")!;

        BinaryNode root = Assert.IsType<BinaryNode>(expression.Tree);
        Assert.Equal("&&", root.Operator);
        BinaryNode comparison = Assert.IsType<BinaryNode>(root.Left);
        Assert.IsType<int>(Assert.IsType<LiteralNode>(comparison.Right).Value);
    }

    public sealed record DrinkView(
        string Name,
        string Category,
        int Price,
        bool Active,
        DateTimeOffset Created,
        TimeSpan Age,
        string[] Tags);
}
