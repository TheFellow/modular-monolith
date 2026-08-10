using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Orders.Models;

namespace Mixology.Modules.Orders.Requests;

public sealed record PlaceOrderItem(DrinkId DrinkId, int Quantity, string Notes = "")
{
    public OrderItem Normalize() => new OrderItem(DrinkId, Quantity, Notes).Normalize();
}

public sealed record PlaceOrderRequest(MenuId MenuId, IReadOnlyList<PlaceOrderItem> Items, string Notes = "")
{
    public PlaceOrderRequest Normalize()
    {
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

        return this with
        {
            Items = Items.Select(item => new PlaceOrderItem(
                item.DrinkId,
                item.Quantity,
                item.Notes?.Trim() ?? string.Empty)).ToArray(),
            Notes = Notes?.Trim() ?? string.Empty,
        };
    }
}

public sealed record ListOrdersRequest(
    OrderStatus? Status = null,
    MenuId? MenuId = null,
    string? Filter = null,
    Cursor Cursor = default,
    int Limit = 0)
{
    public int EffectiveLimit => Limit == 0 ? PageRequest.DefaultLimit : Limit;
    public ListOrdersRequest Normalize()
    {
        Status?.Validate();
        if (MenuId is { } menuId)
        {
            _ = Mixology.Kernel.Entities.MenuId.Parse(menuId.Value);
        }

        if (Limit < 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }

        if (!Cursor.IsEmpty)
        {
            _ = OrderId.Parse(Cursor.Value);
        }

        return this with { Filter = Filter?.Trim(), Limit = EffectiveLimit };
    }
}
