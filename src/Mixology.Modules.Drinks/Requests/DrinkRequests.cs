using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Drinks.Models;

namespace Mixology.Modules.Drinks.Requests;

public sealed record CreateDrinkRequest(
    string Name,
    DrinkCategory Category,
    GlassType Glass,
    Recipe Recipe,
    string Description = "")
{
    public CreateDrinkRequest Normalize()
    {
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("name is required");
        }

        Category.Validate();
        Glass.Validate();
        ArgumentNullException.ThrowIfNull(Recipe);
        return this with
        {
            Name = name,
            Recipe = Recipe.Normalize(),
            Description = Description?.Trim() ?? string.Empty,
        };
    }
}

public sealed record UpdateDrinkRequest(
    DrinkId Id,
    string Name,
    DrinkCategory Category,
    GlassType Glass,
    Recipe Recipe,
    string Description = "")
{
    public UpdateDrinkRequest Normalize()
    {
        if (Id.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = DrinkId.Parse(Id.Value);
        CreateDrinkRequest normalized = new CreateDrinkRequest(Name, Category, Glass, Recipe, Description).Normalize();
        return this with
        {
            Name = normalized.Name,
            Recipe = normalized.Recipe,
            Description = normalized.Description,
        };
    }
}

public sealed record ListDrinksRequest(
    string? Name = null,
    DrinkCategory? Category = null,
    GlassType? Glass = null,
    string? Filter = null,
    Cursor Cursor = default,
    int Limit = 0)
{
    public int EffectiveLimit => Limit == 0 ? PageRequest.DefaultLimit : Limit;

    public ListDrinksRequest Normalize()
    {
        Category?.Validate();
        Glass?.Validate();
        if (Limit < 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }

        if (!Cursor.IsEmpty)
        {
            _ = DrinkId.Parse(Cursor.Value);
        }

        return this with
        {
            Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
            Filter = Filter?.Trim(),
            Limit = EffectiveLimit,
        };
    }
}
