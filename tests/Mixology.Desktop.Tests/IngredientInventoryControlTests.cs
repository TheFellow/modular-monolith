using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mixology.Application.Presentation.Actions;
using Mixology.Desktop.Workspaces.Ingredients;
using Mixology.Desktop.Workspaces.Inventory;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Inventory.Requests;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class IngredientInventoryControlTests
{
    [AvaloniaFact]
    public async Task IngredientViewBindsSemanticListFilterAndScrollableForm()
    {
        Ingredient ingredient = Ingredient("Gin");
        await using IngredientsViewModel viewModel = new(new IngredientOperations(ingredient));
        await viewModel.ActivateAsync();
        IngredientsView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };
        window.Show();
        await UntilAsync(() => view.GetVisualDescendants().OfType<ListBoxItem>().Any());

        Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), box => box.PlaceholderText?.ToString() == "Server filter expression");
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Create"));
        Assert.Contains(view.GetVisualDescendants().OfType<ScrollViewer>(), _ => true);
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Gin");
        window.Close();
    }

    [AvaloniaFact]
    public async Task InventoryViewBindsLowStockAndAdjustmentControls()
    {
        Ingredient ingredient = Ingredient("Gin");
        InventoryStock stock = new(
            InventoryId.New(), ingredient.Id, Amount.Create(8, Unit.Ounce), Amount.Create(2, Unit.Ounce),
            new Price(2m, Currency.Eur), DateTimeOffset.UtcNow, TagCollection.Empty);
        await using InventoryViewModel viewModel = new(new InventoryOperations(new(stock, ingredient)));
        await viewModel.ActivateAsync();
        await UntilAsync(() => viewModel.SelectedInventory is not null && viewModel.CanAdjust);
        viewModel.BeginAdjustCommand.Execute(null);
        InventoryView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };
        window.Show();

        Assert.Contains(view.GetVisualDescendants().OfType<CheckBox>(), box => Equals(box.Content, "Low stock"));
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Delta (blank for cost-only adjustment)");
        Assert.Contains(view.GetVisualDescendants().OfType<ComboBox>(), combo => Equals(combo.SelectedItem, "received"));
        Assert.Contains(view.GetVisualDescendants().OfType<ScrollViewer>(), _ => true);
        window.Close();
    }

    private static Ingredient Ingredient(string name) => new(
        IngredientId.New(), name, IngredientCategory.Spirit, Unit.Ounce, string.Empty, null, TagCollection.Empty);

    private static IReadOnlyList<ActionState> IngredientActions() =>
    [
        new(IngredientActionProjector.ListAction, true, true),
        new(IngredientActionProjector.CreateAction, true, true),
        new(IngredientActionProjector.EditAction, true, true),
        new(IngredientActionProjector.RetireAction, true, true),
        new(IngredientActionProjector.TagsAction, true, true),
    ];

    private static IReadOnlyList<ActionState> InventoryActions() =>
    [
        new(InventoryActionProjector.ListAction, true, true),
        new(InventoryActionProjector.AdjustAction, true, true),
        new(InventoryActionProjector.SetAction, true, true),
        new(InventoryActionProjector.TagsAction, true, true),
    ];

    private static async Task UntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class IngredientOperations(Ingredient ingredient) : IIngredientsDesktopOperations
    {
        public Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken token) =>
            Task.FromResult(new Page<Ingredient>([ingredient], default));
        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken token) => Task.FromResult(ingredient);
        public Task<IReadOnlyList<ActionState>> ProjectAsync(Ingredient? selected, CancellationToken token) =>
            Task.FromResult(IngredientActions());
        public Task<Ingredient> CreateAsync(CreateIngredientRequest request, TagCollection? tags, CancellationToken token) =>
            Task.FromResult(ingredient);
        public Task<Ingredient> UpdateAsync(UpdateIngredientRequest request, TagCollection? tags, CancellationToken token) =>
            Task.FromResult(ingredient);
        public Task<Ingredient> RetireAsync(RetireIngredientRequest request, CancellationToken token) =>
            Task.FromResult(ingredient);
    }

    private sealed class InventoryOperations(InventoryListItemViewModel row) : IInventoryDesktopOperations
    {
        public Task<Page<InventoryListItemViewModel>> ListAsync(ListInventoryRequest request, CancellationToken token) =>
            Task.FromResult(new Page<InventoryListItemViewModel>([row], default));
        public Task<InventoryListItemViewModel> GetAsync(IngredientId id, CancellationToken token) => Task.FromResult(row);
        public Task<IReadOnlyList<ActionState>> ProjectAsync(InventoryStock? selected, CancellationToken token) =>
            Task.FromResult(InventoryActions());
        public Task<InventoryStock> AdjustAsync(AdjustInventoryRequest request, TagCollection? tags, CancellationToken token) =>
            Task.FromResult(row.Stock);
        public Task<InventoryStock> SetAsync(SetInventoryRequest request, TagCollection? tags, CancellationToken token) =>
            Task.FromResult(row.Stock);
    }
}
