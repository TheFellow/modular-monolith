using Mixology.Kernel.Errors;

namespace Mixology.Modules.Drinks.Models;

public readonly record struct GlassType
{
    private GlassType(string value) => Value = value;

    public string Value { get; } = string.Empty;
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static GlassType Rocks { get; } = new("rocks");
    public static GlassType Highball { get; } = new("highball");
    public static GlassType Coupe { get; } = new("coupe");
    public static GlassType Martini { get; } = new("martini");

    public static IReadOnlyList<GlassType> All { get; } = [Rocks, Highball, Coupe, Martini];

    public static GlassType Parse(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized switch
        {
            "" => default,
            "rocks" => Rocks,
            "highball" => Highball,
            "coupe" => Coupe,
            "martini" => Martini,
            _ => throw AppError.Invalid($"invalid glass \"{normalized}\""),
        };
    }

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}
