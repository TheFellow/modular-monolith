using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Money;
using Mixology.Kernel.Tags;

namespace Mixology.Modules.Menus.Models;

public sealed record MenuItem(
    DrinkId DrinkId,
    string? DisplayName,
    Price? Price,
    bool Featured,
    Availability Availability,
    int SortOrder)
{
    public MenuItem Normalize()
    {
        RequireDrinkId(DrinkId);
        Price?.Validate();
        Availability.Validate();
        if (SortOrder < 0)
        {
            throw AppError.Invalid("sort order must be greater than or equal to zero");
        }

        string? displayName = string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim();
        return this with { DisplayName = displayName };
    }

    public void Validate() => _ = Normalize();

    private static void RequireDrinkId(DrinkId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("drink id is required");
        }

        _ = DrinkId.Parse(id.Value);
    }
}

public sealed record Menu(
    MenuId Id,
    string Name,
    string Description,
    IReadOnlyList<MenuItem> Items,
    MenuStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? DeletedAt,
    TagCollection Tags)
{
    public EntityUid EntityUid => Id.EntityUid;
    public bool IsDeleted => DeletedAt.HasValue;

    public Menu Normalize()
    {
        RequireId(Id);
        string name = Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw AppError.Invalid("name is required");
        }

        ArgumentNullException.ThrowIfNull(Items);
        MenuItem[] items = Items.Select(static item => item.Normalize())
            .OrderBy(static item => item.SortOrder)
            .ThenBy(static item => item.DrinkId.Value, StringComparer.Ordinal)
            .ToArray();
        if (items.Select(static item => item.DrinkId).Distinct().Count() != items.Length)
        {
            throw AppError.Invalid("menu contains a duplicate drink");
        }

        if (items.Select(static item => item.SortOrder).Distinct().Count() != items.Length)
        {
            throw AppError.Invalid("menu contains a duplicate sort order");
        }

        Status.Validate();
        ArgumentNullException.ThrowIfNull(Tags);
        Tags.Validate();
        return this with
        {
            Name = name,
            Description = Description?.Trim() ?? string.Empty,
            Items = items,
            CreatedAt = CreatedAt.ToUniversalTime(),
            PublishedAt = PublishedAt?.ToUniversalTime(),
            DeletedAt = DeletedAt?.ToUniversalTime(),
        };
    }

    public void Validate() => _ = Normalize();

    public void RequireDraft()
    {
        if (Status != MenuStatus.Draft)
        {
            throw AppError.FailedPrecondition($"menu {Id} must be a draft");
        }
    }

    public void RequirePublishable()
    {
        RequireDraft();
        if (Items.Count == 0)
        {
            throw AppError.FailedPrecondition($"menu {Id} must contain at least one drink");
        }
    }

    public void RequireReturnToDraft()
    {
        if (Status != MenuStatus.Published)
        {
            throw AppError.FailedPrecondition($"menu {Id} must be published");
        }
    }

    private static void RequireId(MenuId id)
    {
        if (id.IsEmpty)
        {
            throw AppError.Invalid("menu id is required");
        }

        _ = MenuId.Parse(id.Value);
    }
}
