using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Measurement;

public readonly record struct Unit
{
    private Unit(string value)
    {
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public static Unit Ounce { get; } = new("oz");
    public static Unit Milliliter { get; } = new("ml");
    public static Unit Centiliter { get; } = new("cl");
    public static Unit Dash { get; } = new("dash");
    public static Unit Piece { get; } = new("piece");
    public static Unit Splash { get; } = new("splash");

    public static IReadOnlyList<Unit> All { get; } = [Ounce, Milliliter, Centiliter, Dash, Piece, Splash];

    public bool IsVolume => this == Ounce || this == Milliliter || this == Centiliter;

    public bool IsDiscrete => this == Dash || this == Piece || this == Splash;

    public static Unit Parse(string value)
    {
        value = value.Trim();
        return value switch
        {
            "oz" => Ounce,
            "ml" => Milliliter,
            "cl" => Centiliter,
            "dash" => Dash,
            "piece" => Piece,
            "splash" => Splash,
            "" => throw AppError.Invalid("unit is required"),
            _ => throw AppError.Invalid($"invalid unit \"{value}\""),
        };
    }

    public void Validate() => _ = Parse(Value);

    public override string ToString() => Value;
}

