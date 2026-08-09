using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Kernel.Errors;

namespace Mixology.Seed;

public sealed record SeedDataset(
    IReadOnlyList<SeedIngredient> Ingredients,
    IReadOnlyList<SeedDrink> Drinks)
{
    public static SeedDataset LoadEmbedded()
    {
        Assembly assembly = typeof(SeedDataset).Assembly;
        SeedIngredient[] ingredients = Deserialize(
            assembly,
            "Mixology.Seed.Data.ingredients.json",
            SeedJsonContext.Default.SeedIngredientArray,
            "ingredients.json");
        SeedDrink[] drinks = Deserialize(
            assembly,
            "Mixology.Seed.Data.drinks.json",
            SeedJsonContext.Default.SeedDrinkArray,
            "drinks.json");
        return new SeedDataset(ingredients, drinks);
    }

    private static TValue Deserialize<TValue>(
        Assembly assembly,
        string resourceName,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> typeInfo,
        string displayName)
    {
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw AppError.Internal($"embedded seed resource {displayName} was not found");
        try
        {
            return JsonSerializer.Deserialize(stream, typeInfo)
                ?? throw AppError.Invalid($"parse {displayName}: document must not be null");
        }
        catch (JsonException exception)
        {
            throw AppError.Invalid($"parse {displayName}: {exception.Message}", exception);
        }
    }
}

public sealed record SeedIngredient
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Unit { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public required SeedStock Stock { get; init; }
}

public sealed record SeedStock
{
    public required double Quantity { get; init; }
    public required string Cost { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed record SeedDrink
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Glass { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public required SeedRecipe Recipe { get; init; }
}

public sealed record SeedRecipe
{
    public IReadOnlyList<SeedRecipeIngredient> Ingredients { get; init; } = [];
    public IReadOnlyList<string> Steps { get; init; } = [];
    public string Garnish { get; init; } = string.Empty;
}

public sealed record SeedRecipeIngredient
{
    public required string Key { get; init; }
    public required double Amount { get; init; }
    public required string Unit { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SeedIngredient[]))]
[JsonSerializable(typeof(SeedDrink[]))]
internal sealed partial class SeedJsonContext : JsonSerializerContext;
