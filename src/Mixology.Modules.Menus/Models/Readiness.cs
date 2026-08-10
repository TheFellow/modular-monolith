using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;

namespace Mixology.Modules.Menus.Models;

public readonly record struct ReadinessSeverity
{
    private ReadinessSeverity(string value) => Value = value;
    public string Value { get; } = string.Empty;
    public static ReadinessSeverity Blocker { get; } = new("blocker");
    public static ReadinessSeverity Warning { get; } = new("warning");
    public static ReadinessSeverity Parse(string? value) => value?.Trim() switch
    {
        "blocker" => Blocker,
        "warning" => Warning,
        _ => throw AppError.Invalid($"invalid readiness severity \"{value?.Trim()}\""),
    };

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}

public readonly record struct ReadinessCode
{
    private ReadinessCode(string value) => Value = value;
    public string Value { get; } = string.Empty;
    public static ReadinessCode ReviewRequiredDrink { get; } = new("review_required_drink");
    public static ReadinessCode RetiredOrMissingIngredient { get; } = new("retired_or_missing_ingredient");
    public static ReadinessCode TemporarySubstitution { get; } = new("temporary_substitution");
    public static ReadinessCode Unavailable { get; } = new("unavailable");
    public static ReadinessCode LowStock { get; } = new("low_stock");
    public static ReadinessCode Parse(string? value) => value?.Trim() switch
    {
        "review_required_drink" => ReviewRequiredDrink,
        "retired_or_missing_ingredient" => RetiredOrMissingIngredient,
        "temporary_substitution" => TemporarySubstitution,
        "unavailable" => Unavailable,
        "low_stock" => LowStock,
        _ => throw AppError.Invalid($"invalid readiness code \"{value?.Trim()}\""),
    };

    public void Validate() => _ = Parse(Value);
    public override string ToString() => Value;
}

public sealed record ReadinessFinding(
    ReadinessSeverity Severity,
    ReadinessCode Code,
    DrinkId DrinkId,
    IngredientId? IngredientId,
    string Message);

public sealed record ReadinessReport(
    MenuId MenuId,
    MenuStatus Status,
    IReadOnlyList<ReadinessFinding> Findings)
{
    public bool HasBlockers => Findings.Any(static finding => finding.Severity == ReadinessSeverity.Blocker);

    public void RequireReady()
    {
        string[] blockers = Findings
            .Where(static finding => finding.Severity == ReadinessSeverity.Blocker)
            .Select(static finding => finding.Message)
            .ToArray();
        if (blockers.Length != 0)
        {
            throw AppError.FailedPrecondition(
                $"menu \"{MenuId}\" is not ready to publish: {string.Join("; ", blockers)}");
        }
    }
}
