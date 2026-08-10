using Mixology.Kernel.Errors;

namespace Mixology.Modules.Ingredients.Models;

public readonly record struct IngredientCategory
{
    private IngredientCategory(string value)
    {
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static IngredientCategory Spirit { get; } = new("spirit");
    public static IngredientCategory Mixer { get; } = new("mixer");
    public static IngredientCategory Garnish { get; } = new("garnish");
    public static IngredientCategory Bitter { get; } = new("bitter");
    public static IngredientCategory Syrup { get; } = new("syrup");
    public static IngredientCategory Juice { get; } = new("juice");
    public static IngredientCategory Other { get; } = new("other");

    public static IReadOnlyList<IngredientCategory> All { get; } =
    [
        Spirit,
        Mixer,
        Garnish,
        Bitter,
        Syrup,
        Juice,
        Other,
    ];

    public static IngredientCategory Parse(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized switch
        {
            "spirit" => Spirit,
            "mixer" => Mixer,
            "garnish" => Garnish,
            "bitter" => Bitter,
            "syrup" => Syrup,
            "juice" => Juice,
            "other" => Other,
            "" => throw AppError.Invalid("category is required"),
            _ => throw AppError.Invalid($"invalid category \"{normalized}\""),
        };
    }

    public static bool TryParse(string? value, out IngredientCategory category)
    {
        try
        {
            category = Parse(value);
            return true;
        }
        catch (InvalidError)
        {
            category = default;
            return false;
        }
    }

    public void Validate() => _ = Parse(Value);

    public override string ToString() => Value;
}
