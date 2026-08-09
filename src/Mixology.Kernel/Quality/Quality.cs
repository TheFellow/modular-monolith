using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Quality;

public readonly record struct Quality
{
    private Quality(string value)
    {
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public static Quality Equivalent { get; } = new("equivalent");
    public static Quality Similar { get; } = new("similar");
    public static Quality Different { get; } = new("different");

    public int Rank => this == Equivalent ? 3 : this == Similar ? 2 : this == Different ? 1 : 0;

    public static Quality Parse(string value) => value switch
    {
        "equivalent" => Equivalent,
        "similar" => Similar,
        "different" => Different,
        _ => throw AppError.Invalid($"invalid quality \"{value}\""),
    };

    public void Validate() => _ = Parse(Value);

    public override string ToString() => Value;
}

