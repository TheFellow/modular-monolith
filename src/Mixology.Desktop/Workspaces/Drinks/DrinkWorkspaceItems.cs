using CommunityToolkit.Mvvm.ComponentModel;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Modules.Drinks.Models;

namespace Mixology.Desktop.Workspaces.Drinks;

public sealed record DrinkListItemViewModel(
    DrinkId Id,
    string Name,
    string Category,
    string Glass,
    string Status,
    string Tags)
{
    public static DrinkListItemViewModel FromDrink(Drink drink) => new(
        drink.Id,
        drink.Name,
        drink.Category.Value,
        drink.Glass.Value,
        drink.Status.Value,
        drink.Tags.Count == 0 ? string.Empty : drink.Tags.Format());
}

public sealed record IngredientOptionViewModel(IngredientId Id, string Name)
{
    public string Display => $"{Name} · {Id.Value}";
}

public sealed partial class DrinkActionViewModel : ObservableObject
{
    public DrinkActionViewModel(ActionState state)
    {
        Id = state.Id;
        Visible = state.Visible;
        Enabled = state.Enabled;
        DisabledReason = state.DisabledReason;
    }

    public ActionId Id { get; }

    public bool Visible { get; }

    public bool Enabled { get; }

    public string DisabledReason { get; }

    public string AccessibilityDescription => Enabled ? string.Empty : DisabledReason;
}
