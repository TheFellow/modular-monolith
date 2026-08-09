using Mixology.Kernel.Errors;

namespace Mixology.Modules.Drinks.Models;

public readonly record struct DrinkStatus
{
    private DrinkStatus(string value) => Value = value;

    public string Value { get; } = string.Empty;

    public static DrinkStatus Active { get; } = new("active");
    public static DrinkStatus ReviewRequired { get; } = new("review_required");
    public static IReadOnlyList<DrinkStatus> All { get; } = [Active, ReviewRequired];

    public static DrinkStatus Parse(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized switch
        {
            "active" => Active,
            "review_required" => ReviewRequired,
            _ => throw AppError.Invalid($"invalid drink status \"{normalized}\""),
        };
    }

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}
