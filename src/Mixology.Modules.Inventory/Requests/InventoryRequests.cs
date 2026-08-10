using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Modules.Inventory.Models;

namespace Mixology.Modules.Inventory.Requests;

public sealed record SetInventoryRequest(IngredientId IngredientId, Amount OnHand, Price UnitCost)
{
    public SetInventoryRequest Normalize()
    {
        RequireIngredientId(IngredientId);
        ArgumentNullException.ThrowIfNull(OnHand);
        OnHand.Unit.Validate();
        RequireFinite(OnHand.Value, "on-hand quantity");
        UnitCost.Validate();
        return this;
    }

    public void Validate() => _ = Normalize();

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw AppError.Invalid($"{name} must be finite");
        }
    }

    private static void RequireIngredientId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }
}

public sealed record AdjustInventoryRequest(
    IngredientId IngredientId,
    AdjustmentReason Reason,
    Amount? Delta = null,
    Price? UnitCost = null)
{
    public AdjustInventoryRequest Normalize()
    {
        if (IngredientId.IsEmpty)
        {
            throw AppError.Invalid("ingredient id is required");
        }

        _ = IngredientId.Parse(IngredientId.Value);
        Reason.Validate();
        if (Delta is null && UnitCost is null)
        {
            throw AppError.Invalid("at least one of delta or unit cost is required");
        }

        if (Delta is { } delta)
        {
            delta.Unit.Validate();
            if (!double.IsFinite(delta.Value))
            {
                throw AppError.Invalid("delta must be finite");
            }
        }

        UnitCost?.Validate();
        return this;
    }

    public void Validate() => _ = Normalize();
}

public sealed record ListInventoryRequest(
    IngredientId? IngredientId = null,
    double? LowStock = null,
    string? Filter = null,
    Cursor Cursor = default,
    int Limit = 0)
{
    public const double DefaultLowStockThreshold = 10d;

    public int EffectiveLimit => Limit == 0 ? PageRequest.DefaultLimit : Limit;

    public ListInventoryRequest Normalize()
    {
        if (IngredientId is { } ingredientId)
        {
            if (ingredientId.IsEmpty)
            {
                throw AppError.Invalid("ingredient id is required");
            }

            _ = Mixology.Kernel.Entities.IngredientId.Parse(ingredientId.Value);
        }

        if (LowStock is { } threshold && !double.IsFinite(threshold))
        {
            throw AppError.Invalid("low-stock threshold must be finite");
        }

        if (Limit < 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }

        if (!Cursor.IsEmpty)
        {
            _ = InventoryId.Parse(Cursor.Value);
        }

        return this with { Filter = Filter?.Trim(), Limit = EffectiveLimit };
    }

    public void Validate() => _ = Normalize();
}
