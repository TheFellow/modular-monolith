using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Tags;

namespace Mixology.Modules.Inventory.Models;

public sealed record InventoryStock(
    InventoryId Id,
    IngredientId IngredientId,
    Amount OnHand,
    Amount Reserved,
    Price? UnitCost,
    DateTimeOffset LastUpdated,
    TagCollection Tags,
    long Revision = 1)
{
    public EntityUid EntityUid => Id.EntityUid;

    public Amount Available
    {
        get
        {
            Amount reserved = Reserved.Convert(OnHand.Unit);
            Amount available = OnHand.Subtract(reserved);
            return available.Value < 0d ? Amount.Create(0d, OnHand.Unit) : available;
        }
    }

    public InventoryStock Normalize()
    {
        RequireId(Id);
        RequireIngredientId(IngredientId);
        ArgumentNullException.ThrowIfNull(OnHand);
        ArgumentNullException.ThrowIfNull(Reserved);
        OnHand.Unit.Validate();
        Reserved.Unit.Validate();
        if (!double.IsFinite(OnHand.Value) || OnHand.Value < 0d)
        {
            throw AppError.Invalid("on-hand quantity must be a finite value greater than or equal to zero");
        }

        if (!double.IsFinite(Reserved.Value) || Reserved.Value < 0d)
        {
            throw AppError.Invalid("reserved quantity must be a finite value greater than or equal to zero");
        }

        Amount normalizedReserved = Reserved.Convert(OnHand.Unit);
        UnitCost?.Validate();
        ArgumentNullException.ThrowIfNull(Tags);
        Tags.Validate();
        if (Revision <= 0)
        {
            throw AppError.Invalid("revision must be greater than zero");
        }
        return this with
        {
            Reserved = normalizedReserved,
            LastUpdated = LastUpdated.ToUniversalTime(),
        };
    }

    public void Validate() => _ = Normalize();

    private static void RequireId(InventoryId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("inventory id is required");
        }

        _ = InventoryId.Parse(id.Value);
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
