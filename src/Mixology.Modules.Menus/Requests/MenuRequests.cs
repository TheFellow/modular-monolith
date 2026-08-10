using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Menus.Models;

namespace Mixology.Modules.Menus.Requests;

public sealed record CreateMenuRequest(string Name, string Description = "")
{
    public CreateMenuRequest Normalize()
    {
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("name is required");
        }

        return this with { Name = name, Description = Description?.Trim() ?? string.Empty };
    }
}

public sealed record UpdateMenuRequest(MenuId Id, string Name, string Description = "")
{
    public UpdateMenuRequest Normalize()
    {
        RequireMenuId(Id);
        CreateMenuRequest normalized = new CreateMenuRequest(Name, Description).Normalize();
        return this with { Name = normalized.Name, Description = normalized.Description };
    }

    private static void RequireMenuId(MenuId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("menu id is required");
        }

        _ = MenuId.Parse(id.Value);
    }
}

public sealed record AddMenuItemRequest(MenuId MenuId, DrinkId DrinkId)
{
    public AddMenuItemRequest Normalize()
    {
        RequireMenuId(MenuId);
        if (DrinkId.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = DrinkId.Parse(DrinkId.Value);
        return this;
    }

    private static void RequireMenuId(MenuId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("menu id is required");
        }

        _ = MenuId.Parse(id.Value);
    }
}

public sealed record RemoveMenuItemRequest(MenuId MenuId, DrinkId DrinkId)
{
    public RemoveMenuItemRequest Normalize()
    {
        _ = new AddMenuItemRequest(MenuId, DrinkId).Normalize();
        return this;
    }
}

public sealed record ListMenusRequest(
    MenuStatus? Status = null,
    string? Filter = null,
    Cursor Cursor = default,
    int Limit = 0)
{
    public int EffectiveLimit => Limit == 0 ? PageRequest.DefaultLimit : Limit;

    public ListMenusRequest Normalize()
    {
        Status?.Validate();
        if (Limit < 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }

        if (!Cursor.IsEmpty)
        {
            _ = MenuId.Parse(Cursor.Value);
        }

        return this with { Filter = Filter?.Trim(), Limit = EffectiveLimit };
    }
}
