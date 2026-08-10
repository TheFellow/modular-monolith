using Mixology.Kernel.Errors;

namespace Mixology.Modules.Inventory.Models;

public readonly record struct AdjustmentReason
{
    private AdjustmentReason(string value)
    {
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static AdjustmentReason Received { get; } = new("received");
    public static AdjustmentReason Used { get; } = new("used");
    public static AdjustmentReason Spilled { get; } = new("spilled");
    public static AdjustmentReason Expired { get; } = new("expired");
    public static AdjustmentReason Corrected { get; } = new("corrected");

    public static IReadOnlyList<AdjustmentReason> All { get; } =
    [
        Received,
        Used,
        Spilled,
        Expired,
        Corrected,
    ];

    public static AdjustmentReason Parse(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "received" => Received,
            "used" => Used,
            "spilled" => Spilled,
            "expired" => Expired,
            "corrected" => Corrected,
            "" => throw AppError.Invalid("adjustment reason is required"),
            _ => throw AppError.Invalid($"invalid adjustment reason \"{normalized}\""),
        };
    }

    public void Validate() => _ = Parse(Value);

    public override string ToString() => Value;
}
