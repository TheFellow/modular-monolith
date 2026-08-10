using Mixology.Kernel.Errors;

namespace Mixology.Modules.Drinks.Models;

public readonly record struct DrinkCategory
{
    private DrinkCategory(string value) => Value = value;

    public string Value { get; } = string.Empty;
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static DrinkCategory Cocktail { get; } = new("cocktail");
    public static DrinkCategory Mocktail { get; } = new("mocktail");
    public static DrinkCategory Wine { get; } = new("wine");
    public static DrinkCategory Shot { get; } = new("shot");
    public static DrinkCategory Highball { get; } = new("highball");
    public static DrinkCategory Martini { get; } = new("martini");
    public static DrinkCategory Sour { get; } = new("sour");
    public static DrinkCategory Tiki { get; } = new("tiki");

    public static IReadOnlyList<DrinkCategory> All { get; } =
    [Cocktail, Mocktail, Wine, Shot, Highball, Martini, Sour, Tiki];

    public static DrinkCategory Parse(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized switch
        {
            "" => default,
            "cocktail" => Cocktail,
            "mocktail" => Mocktail,
            "wine" => Wine,
            "shot" => Shot,
            "highball" => Highball,
            "martini" => Martini,
            "sour" => Sour,
            "tiki" => Tiki,
            _ => throw AppError.Invalid($"invalid category \"{normalized}\""),
        };
    }

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}
