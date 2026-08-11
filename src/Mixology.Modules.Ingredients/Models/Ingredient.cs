using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Tags;

namespace Mixology.Modules.Ingredients.Models;

public sealed record Ingredient(
    IngredientId Id,
    string Name,
    IngredientCategory Category,
    Unit Unit,
    string Description,
    DateTimeOffset? DeletedAt,
    TagCollection Tags,
    long Revision = 1)
{
    public EntityUid EntityUid => Id.EntityUid;

    public bool IsRetired => DeletedAt.HasValue;

    public Ingredient Normalize()
    {
        ValidateId(Id);
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("name is required");
        }

        Category.Validate();
        Unit.Validate();
        ArgumentNullException.ThrowIfNull(Tags);
        Tags.Validate();
        if (Revision <= 0)
        {
            throw AppError.Invalid("revision must be greater than zero");
        }

        DateTimeOffset? deletedAt = DeletedAt?.ToUniversalTime();
        return this with
        {
            Name = name,
            Description = Description?.Trim() ?? string.Empty,
            DeletedAt = deletedAt,
        };
    }

    public void Validate() => _ = Normalize();

    private static void ValidateId(IngredientId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("id is required");
        }

        _ = IngredientId.Parse(id.Value);
    }
}
