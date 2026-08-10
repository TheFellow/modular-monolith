using Mixology.Kernel.Errors;

namespace Mixology.Modules.Orders.Models;

public readonly record struct OrderStatus
{
    private OrderStatus(string value) => Value = value;

    public string Value { get; } = string.Empty;

    public static OrderStatus Pending { get; } = new("pending");
    public static OrderStatus Blocked { get; } = new("blocked");
    public static OrderStatus Completed { get; } = new("completed");
    public static OrderStatus Cancelled { get; } = new("cancelled");

    public static IReadOnlyList<OrderStatus> All { get; } = [Pending, Blocked, Completed, Cancelled];

    public static OrderStatus Parse(string? value) => value?.Trim() switch
    {
        "pending" => Pending,
        "blocked" => Blocked,
        "completed" => Completed,
        "cancelled" => Cancelled,
        _ => throw AppError.Invalid($"invalid order status \"{value?.Trim()}\""),
    };

    public void Validate() => _ = Parse(Value);

    public override string ToString() => Value;
}
