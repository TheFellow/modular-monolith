using Mixology.Kernel.Errors;

namespace Mixology.Modules.Menus.Models;

public readonly record struct MenuStatus
{
    private MenuStatus(string value) => Value = value;

    public string Value { get; } = string.Empty;

    public static MenuStatus Draft { get; } = new("draft");
    public static MenuStatus Published { get; } = new("published");
    public static MenuStatus Archived { get; } = new("archived");
    public static IReadOnlyList<MenuStatus> All { get; } = [Draft, Published, Archived];

    public static MenuStatus Parse(string? value) => value?.Trim() switch
    {
        "draft" => Draft,
        "published" => Published,
        "archived" => Archived,
        _ => throw AppError.Invalid($"invalid menu status \"{value?.Trim()}\""),
    };

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}

public readonly record struct Availability
{
    private Availability(string value) => Value = value;

    public string Value { get; } = string.Empty;

    public static Availability Available { get; } = new("available");
    public static Availability Limited { get; } = new("limited");
    public static Availability Unavailable { get; } = new("unavailable");
    public static IReadOnlyList<Availability> All { get; } = [Available, Limited, Unavailable];

    public static Availability Parse(string? value) => value?.Trim() switch
    {
        "available" => Available,
        "limited" => Limited,
        "unavailable" => Unavailable,
        _ => throw AppError.Invalid($"invalid availability \"{value?.Trim()}\""),
    };

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}
