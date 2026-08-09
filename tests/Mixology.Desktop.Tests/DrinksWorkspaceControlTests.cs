using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mixology.Application.Presentation.Actions;
using Mixology.Desktop.Workspaces.Drinks;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients.Models;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class DrinksWorkspaceControlTests
{
    [AvaloniaFact]
    public async Task ViewExposesCompiledSemanticScrollableStructuredRecipeControls()
    {
        Ingredient ingredient = new(
            IngredientId.New(),
            "Gin",
            IngredientCategory.Spirit,
            Unit.Ounce,
            string.Empty,
            null,
            TagCollection.Empty);
        await using DrinksWorkspaceViewModel viewModel = new(new ControlsOperations(ingredient));
        await viewModel.ActivateAsync();
        await viewModel.StartCreateAsync();
        DrinksWorkspaceView view = new() { DataContext = viewModel };
        Window window = new() { Content = view, Width = 1200, Height = 800 };

        window.Show();
        TextBox name = Named<TextBox>(window, "Drink name");
        name.Text = "Negroni";
        Assert.Equal("Negroni", viewModel.Name);
        Assert.True(viewModel.IsDirty);
        Assert.NotNull(Named<ComboBox>(window, "Recipe ingredient picker"));
        Assert.NotNull(Named<TextBox>(window, "Recipe amount"));
        Assert.NotNull(Named<ComboBox>(window, "Recipe unit"));
        Assert.NotNull(Named<CheckBox>(window, "Optional recipe ingredient"));
        Assert.NotNull(Named<Button>(window, "Add recipe ingredient"));
        Assert.NotNull(Named<Button>(window, "Add recipe step"));
        Assert.NotNull(Named<TextBox>(window, "Drink garnish"));
        ScrollViewer editor = Named<ScrollViewer>(window, "Drink editor");
        Assert.True(editor.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task DisabledProjectedActionsRemainVisibleWithReason()
    {
        Ingredient ingredient = new(
            IngredientId.New(),
            "Gin",
            IngredientCategory.Spirit,
            Unit.Ounce,
            string.Empty,
            null,
            TagCollection.Empty);
        await using DrinksWorkspaceViewModel viewModel = new(new ControlsOperations(ingredient, denyCreate: true));
        await viewModel.ActivateAsync();
        DrinksWorkspaceView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();
        Button create = Named<Button>(window, "Create drink");
        Assert.True(create.IsVisible);
        Assert.False(create.IsEnabled);
        Assert.Equal("manager role required", AutomationProperties.GetHelpText(create));
        window.Close();
    }

    private static T Named<T>(Control root, string name)
        where T : Control => Assert.Single(
            root.GetVisualDescendants().OfType<T>(),
            control => AutomationProperties.GetName(control) == name);

    private sealed class ControlsOperations(Ingredient ingredient, bool denyCreate = false)
        : IDrinksWorkspaceOperations
    {
        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new Page<Drink>([], default));

        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            Drink? selected,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>(
            [
                new(DrinkActionProjector.ListAction, true, true),
                new(
                    DrinkActionProjector.CreateAction,
                    true,
                    !denyCreate,
                    denyCreate ? "manager role required" : string.Empty),
            ]);

        public Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Ingredient>>([ingredient]);

        public Task<Drink> CreateAsync(
            CreateDrinkRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Drink> UpdateAsync(
            UpdateDrinkRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
