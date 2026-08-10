using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;

namespace Mixology.Modules.Drinks.Models;

public sealed record Drink(
    DrinkId Id,
    string Name,
    DrinkCategory Category,
    GlassType Glass,
    Recipe Recipe,
    string Description,
    DrinkStatus Status,
    DateTimeOffset? DeletedAt,
    TagCollection Tags)
{
    public EntityUid EntityUid => Id.EntityUid;
    public bool IsDeleted => DeletedAt.HasValue;

    public Drink Normalize()
    {
        if (Id.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = DrinkId.Parse(Id.Value);
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("name is required");
        }

        Category.Validate();
        Glass.Validate();
        Status.Validate();
        ArgumentNullException.ThrowIfNull(Recipe);
        ArgumentNullException.ThrowIfNull(Tags);
        Tags.Validate();
        return this with
        {
            Name = name,
            Recipe = Recipe.Normalize(),
            Description = Description?.Trim() ?? string.Empty,
            DeletedAt = DeletedAt?.ToUniversalTime(),
        };
    }

    public void Validate() => _ = Normalize();
}
