using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Modules.Drinks.Models;

namespace Mixology.Gui.Workspaces.Drinks;

public sealed class SubstituteOptionViewModel : ObservableObject
{
    private readonly Action changed;
    private bool isSelected;

    public SubstituteOptionViewModel(IngredientOptionViewModel option, bool selected, Action changed)
    {
        Option = option;
        isSelected = selected;
        this.changed = changed;
    }

    public IngredientOptionViewModel Option { get; }

    public string Name => Option.Name;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value))
            {
                changed();
            }
        }
    }
}

public sealed class RecipeStepViewModel : ObservableObject
{
    private readonly Action changed;
    private string text;

    internal RecipeStepViewModel(
        string text,
        Action changed,
        Action<RecipeStepViewModel> remove,
        Action<RecipeStepViewModel, int> move)
    {
        this.text = text;
        this.changed = changed;
        RemoveCommand = new RelayCommand(() => remove(this));
        MoveUpCommand = new RelayCommand(() => move(this, -1));
        MoveDownCommand = new RelayCommand(() => move(this, 1));
    }

    public IRelayCommand RemoveCommand { get; }

    public IRelayCommand MoveUpCommand { get; }

    public IRelayCommand MoveDownCommand { get; }

    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, value ?? string.Empty))
            {
                changed();
            }
        }
    }
}

public sealed class RecipeIngredientViewModel : ObservableObject
{
    private readonly Action changed;
    private IReadOnlyList<IngredientOptionViewModel> catalog = [];
    private IngredientOptionViewModel? selectedIngredient;
    private string ingredientSearch = string.Empty;
    private string amount = string.Empty;
    private string selectedUnit = Unit.Ounce.Value;
    private bool isOptional;

    internal RecipeIngredientViewModel(
        RecipeIngredient? ingredient,
        Action changed,
        Action<RecipeIngredientViewModel> remove)
    {
        this.changed = changed;
        IngredientId = ingredient?.IngredientId ?? default;
        amount = ingredient?.Amount.Value.ToString("G", CultureInfo.InvariantCulture) ?? string.Empty;
        selectedUnit = ingredient?.Amount.Unit.Value ?? Unit.Ounce.Value;
        isOptional = ingredient?.Optional ?? false;
        SubstituteIds = ingredient?.Substitutes.ToHashSet() ?? [];
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    internal IngredientId IngredientId { get; private set; }

    internal HashSet<IngredientId> SubstituteIds { get; }

    public ObservableCollection<IngredientOptionViewModel> IngredientMatches { get; } = [];

    public ObservableCollection<SubstituteOptionViewModel> SubstituteOptions { get; } = [];

    public IRelayCommand RemoveCommand { get; }

    public IReadOnlyList<string> Units { get; } = Unit.All.Select(static value => value.Value).ToArray();

    public string IngredientSearch
    {
        get => ingredientSearch;
        set
        {
            if (SetProperty(ref ingredientSearch, value ?? string.Empty))
            {
                if (selectedIngredient is not null &&
                    !string.Equals(ingredientSearch.Trim(), selectedIngredient.Name, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ingredientSearch.Trim(), selectedIngredient.Id.Value, StringComparison.Ordinal))
                {
                    selectedIngredient = null;
                    IngredientId = default;
                    OnPropertyChanged(nameof(SelectedIngredient));
                    RebuildSubstitutes();
                }

                FilterCatalog();
                changed();
            }
        }
    }

    public IngredientOptionViewModel? SelectedIngredient
    {
        get => selectedIngredient;
        set
        {
            if (!SetProperty(ref selectedIngredient, value) || value is null)
            {
                return;
            }

            IngredientId = value.Id;
            ingredientSearch = value.Name;
            OnPropertyChanged(nameof(IngredientSearch));
            _ = SubstituteIds.Remove(value.Id);
            RebuildSubstitutes();
            changed();
        }
    }

    public string Amount
    {
        get => amount;
        set
        {
            if (SetProperty(ref amount, value ?? string.Empty))
            {
                changed();
            }
        }
    }

    public string SelectedUnit
    {
        get => selectedUnit;
        set
        {
            if (SetProperty(ref selectedUnit, value ?? string.Empty))
            {
                changed();
            }
        }
    }

    public bool IsOptional
    {
        get => isOptional;
        set
        {
            if (SetProperty(ref isOptional, value))
            {
                changed();
            }
        }
    }

    internal void SetCatalog(IReadOnlyList<IngredientOptionViewModel> options)
    {
        catalog = options;
        selectedIngredient = catalog.SingleOrDefault(value => value.Id == IngredientId);
        if (selectedIngredient is not null)
        {
            ingredientSearch = selectedIngredient.Name;
        }

        OnPropertyChanged(nameof(SelectedIngredient));
        OnPropertyChanged(nameof(IngredientSearch));
        FilterCatalog();
        RebuildSubstitutes();
    }

    internal RecipeIngredient Build(int index)
    {
        if (IngredientId.IsEmpty)
        {
            throw AppError.Invalid($"recipe ingredient {index}: choose an ingredient");
        }

        if (!double.TryParse(Amount, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw AppError.Invalid($"recipe ingredient {index}: amount must be a number");
        }

        return new RecipeIngredient(
            IngredientId,
            Mixology.Kernel.Measurement.Amount.Create(value, Unit.Parse(SelectedUnit)),
            IsOptional,
            SubstituteOptions.Where(static option => option.IsSelected).Select(static option => option.Option.Id));
    }

    private void FilterCatalog()
    {
        string query = IngredientSearch.Trim();
        IngredientOptionViewModel[] matches = catalog
            .Where(option => query.Length == 0
                || option.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.Id.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .ToArray();
        IngredientMatches.Clear();
        foreach (IngredientOptionViewModel option in matches)
        {
            IngredientMatches.Add(option);
        }
    }

    private void RebuildSubstitutes()
    {
        SubstituteOptions.Clear();
        foreach (IngredientOptionViewModel option in catalog.Where(value => value.Id != IngredientId))
        {
            SubstituteOptions.Add(new SubstituteOptionViewModel(
                option,
                SubstituteIds.Contains(option.Id),
                () => SubstituteChanged(option.Id)));
        }
    }

    private void SubstituteChanged(IngredientId id)
    {
        SubstituteOptionViewModel option = SubstituteOptions.Single(value => value.Option.Id == id);
        if (option.IsSelected)
        {
            _ = SubstituteIds.Add(id);
        }
        else
        {
            _ = SubstituteIds.Remove(id);
        }

        changed();
    }
}

public sealed class DrinkRecipeEditorViewModel : ObservableObject
{
    private readonly Action changed;
    private IReadOnlyList<IngredientOptionViewModel> catalog = [];
    private string garnish;

    public DrinkRecipeEditorViewModel(Recipe? recipe, Action changed)
    {
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
        garnish = recipe?.Garnish ?? string.Empty;
        foreach (RecipeIngredient ingredient in recipe?.Ingredients ?? [])
        {
            Ingredients.Add(new RecipeIngredientViewModel(ingredient, changed, RemoveIngredient));
        }

        foreach (string step in recipe?.Steps ?? [])
        {
            Steps.Add(new RecipeStepViewModel(step, changed, RemoveStep, MoveStep));
        }

        if (Ingredients.Count == 0)
        {
            Ingredients.Add(new RecipeIngredientViewModel(null, changed, RemoveIngredient));
        }

        if (Steps.Count == 0)
        {
            Steps.Add(new RecipeStepViewModel(string.Empty, changed, RemoveStep, MoveStep));
        }

        AddIngredientCommand = new RelayCommand(AddIngredient);
        AddStepCommand = new RelayCommand(AddStep);
    }

    public ObservableCollection<RecipeIngredientViewModel> Ingredients { get; } = [];

    public ObservableCollection<RecipeStepViewModel> Steps { get; } = [];

    public IRelayCommand AddIngredientCommand { get; }

    public IRelayCommand AddStepCommand { get; }

    public string Garnish
    {
        get => garnish;
        set
        {
            if (SetProperty(ref garnish, value ?? string.Empty))
            {
                changed();
            }
        }
    }

    public void SetCatalog(IReadOnlyList<IngredientOptionViewModel> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        catalog = options;
        foreach (RecipeIngredientViewModel row in Ingredients)
        {
            row.SetCatalog(options);
        }
    }

    public void RemoveIngredient(RecipeIngredientViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (Ingredients.Count == 1)
        {
            return;
        }

        _ = Ingredients.Remove(row);
        changed();
    }

    public void RemoveStep(RecipeStepViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (Steps.Count == 1)
        {
            return;
        }

        _ = Steps.Remove(row);
        changed();
    }

    public void MoveStep(RecipeStepViewModel row, int delta)
    {
        ArgumentNullException.ThrowIfNull(row);
        int current = Steps.IndexOf(row);
        int target = Math.Clamp(current + delta, 0, Steps.Count - 1);
        if (target == current)
        {
            return;
        }

        Steps.Move(current, target);
        changed();
    }

    public Recipe Build() => new Recipe(
        Ingredients.Select(static (row, index) => row.Build(index)),
        Steps.Select(static value => value.Text),
        Garnish).Normalize();

    private void AddIngredient()
    {
        RecipeIngredientViewModel row = new(null, changed, RemoveIngredient);
        Ingredients.Add(row);
        row.SetCatalog(catalog);
        changed();
    }

    private void AddStep()
    {
        Steps.Add(new RecipeStepViewModel(string.Empty, changed, RemoveStep, MoveStep));
        changed();
    }
}
