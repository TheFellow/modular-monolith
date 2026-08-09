using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;

namespace Mixology.Modules.Orders.Models;

public sealed record OrderItem(DrinkId DrinkId, int Quantity, string Notes)
{
    public OrderItem Normalize()
    {
        if (DrinkId.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = Mixology.Kernel.Entities.DrinkId.Parse(DrinkId.Value);
        if (Quantity <= 0)
        {
            throw AppError.Invalid("quantity must be greater than zero");
        }

        return this with { Notes = Notes?.Trim() ?? string.Empty };
    }
}

/// <summary>An immutable fulfillment snapshot captured when an order is accepted.</summary>
public sealed record IngredientUsage(IngredientId IngredientId, string Name, Amount Amount)
{
    public IngredientUsage Normalize()
    {
        if (IngredientId.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = Mixology.Kernel.Entities.IngredientId.Parse(IngredientId.Value);
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("ingredient name is required");
        }

        ArgumentNullException.ThrowIfNull(Amount);
        Amount.Unit.Validate();
        if (!double.IsFinite(Amount.Value) || Amount.Value <= 0d)
        {
            throw AppError.Invalid("ingredient usage amount must be greater than zero");
        }

        return this with { Name = name };
    }
}

public sealed record Order(
    OrderId Id,
    MenuId MenuId,
    IReadOnlyList<OrderItem> Items,
    IReadOnlyList<IngredientUsage> IngredientUsage,
    IReadOnlyList<IngredientId> BlockedIngredientIds,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string Notes,
    DateTimeOffset? DeletedAt,
    TagCollection Tags)
{
    public EntityUid EntityUid => Id.EntityUid;

    public Order Normalize()
    {
        if (Id.IsEmpty)
        {
            throw AppError.Invalid("order id is required");
        }

        _ = OrderId.Parse(Id.Value);
        if (MenuId.IsEmpty)
        {
            throw AppError.Invalid("menu id is required");
        }

        _ = Mixology.Kernel.Entities.MenuId.Parse(MenuId.Value);
        ArgumentNullException.ThrowIfNull(Items);
        if (Items.Count == 0)
        {
            throw AppError.Invalid("order must have at least one item");
        }

        OrderItem[] items = Items.Select(static item => item.Normalize()).ToArray();
        ArgumentNullException.ThrowIfNull(IngredientUsage);
        IngredientUsage[] usage = IngredientUsage.Select(static value => value.Normalize())
            .OrderBy(static value => value.IngredientId.Value, StringComparer.Ordinal).ToArray();
        ArgumentNullException.ThrowIfNull(BlockedIngredientIds);
        IngredientId[] blocked = BlockedIngredientIds.Distinct()
            .OrderBy(static id => id.Value, StringComparer.Ordinal).ToArray();
        foreach (IngredientId id in blocked)
        {
            _ = Mixology.Kernel.Entities.IngredientId.Parse(id.Value);
        }

        Status.Validate();
        if (Status == OrderStatus.Completed && CompletedAt is null)
        {
            throw AppError.Invalid("completed order requires a completion timestamp");
        }

        if (Status != OrderStatus.Completed && CompletedAt is not null)
        {
            throw AppError.Invalid("only completed orders may have a completion timestamp");
        }

        ArgumentNullException.ThrowIfNull(Tags);
        Tags.Validate();
        return this with
        {
            Items = items,
            IngredientUsage = usage,
            BlockedIngredientIds = blocked,
            CreatedAt = CreatedAt.ToUniversalTime(),
            CompletedAt = CompletedAt?.ToUniversalTime(),
            DeletedAt = DeletedAt?.ToUniversalTime(),
            Notes = Notes?.Trim() ?? string.Empty,
        };
    }
}
