using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Modules.Ingredients.Models;

namespace Mixology.Modules.Ingredients.Requests;

public sealed record CreateIngredientRequest(
    string Name,
    IngredientCategory Category,
    Unit Unit,
    string Description = "")
{
    public CreateIngredientRequest Normalize()
    {
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("name is required");
        }

        Category.Validate();
        Unit.Validate();
        return this with { Name = name, Description = Description?.Trim() ?? string.Empty };
    }

    public void Validate() => _ = Normalize();
}

public sealed record UpdateIngredientRequest(
    IngredientId Id,
    string? Name = null,
    IngredientCategory? Category = null,
    Unit? Unit = null,
    string? Description = null)
{
    public UpdateIngredientRequest Normalize()
    {
        RequireId(Id);
        string? name = Name?.Trim();
        name = string.IsNullOrEmpty(name) ? null : name;

        Category?.Validate();
        Unit?.Validate();
        string? description = Description?.Trim();
        description = string.IsNullOrEmpty(description) ? null : description;
        return this with { Name = name, Description = description };
    }

    public void Validate() => _ = Normalize();

    private static void RequireId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }
}

public sealed record RetireIngredientRequest(IngredientId Id, Retirement Retirement)
{
    public RetireIngredientRequest Normalize()
    {
        if (Id.IsEmpty)
        {
            throw AppError.Invalid("id is required");
        }

        _ = IngredientId.Parse(Id.Value);
        ArgumentNullException.ThrowIfNull(Retirement);
        Retirement normalized = Retirement.Normalize();
        if (normalized.ReplacementId == Id)
        {
            throw AppError.Invalid("replacement ingredient must differ from retired ingredient");
        }

        return this with { Retirement = normalized };
    }

    public void Validate() => _ = Normalize();
}

public sealed record ListIngredientsRequest(
    IngredientCategory? Category = null,
    string? Filter = null,
    Cursor Cursor = default,
    int Limit = 0)
{
    public int EffectiveLimit => Limit == 0 ? PageRequest.DefaultLimit : Limit;

    public ListIngredientsRequest Normalize()
    {
        Category?.Validate();
        if (Limit < 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }

        if (!Cursor.IsEmpty)
        {
            _ = IngredientId.Parse(Cursor.Value);
        }

        return this with { Filter = Filter?.Trim(), Limit = EffectiveLimit };
    }

    public void Validate() => _ = Normalize();
}
