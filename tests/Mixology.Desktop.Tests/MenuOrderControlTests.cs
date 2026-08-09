using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mixology.Application.Presentation.Actions;
using Mixology.Desktop.Workspaces.Menus;
using Mixology.Desktop.Workspaces.Orders;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;
using Xunit;
using MenuModel = Mixology.Modules.Menus.Models.Menu;

namespace Mixology.Desktop.Tests;

public sealed class MenuOrderControlTests
{
    [AvaloniaFact]
    public async Task MenuViewHasScrollableSemanticLifecycleAndAnalysisControls()
    {
        await using MenusViewModel viewModel = new(new EmptyMenus());
        MenusView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };
        window.Show();

        Assert.Contains(window.GetVisualDescendants().OfType<ScrollViewer>(), _ => true);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Publish") && button.Command is not null);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Analyze") && button.Command is not null);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), text => text.PlaceholderText == "Target margin");
        window.Close();
    }

    [AvaloniaFact]
    public async Task OrderViewHasScrollableSemanticPlacementAndImmutableDetailControls()
    {
        await using OrdersViewModel viewModel = new(new EmptyOrders());
        OrdersView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };
        window.Show();

        Assert.Contains(window.GetVisualDescendants().OfType<ScrollViewer>(), _ => true);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Submit order") && button.Command is not null);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Complete") && button.Command is not null);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), text => text.PlaceholderText == "Order notes");
        window.Close();
    }

    private sealed class EmptyMenus : IMenuDesktopOperations
    {
        public Task<Page<MenuModel>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken) => Task.FromResult(new Page<MenuModel>([], default));
        public Task<MenuModel> GetAsync(MenuId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ActionState>> ProjectAsync(MenuModel? selected, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>([]);
        public Task<IReadOnlyList<Drink>> DrinksAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Drink>>([]);
        public Task<MenuModel> CreateAsync(CreateMenuRequest request, TagCollection tags, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuModel> UpdateAsync(UpdateMenuRequest request, TagCollection tags, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuModel> DeleteAsync(MenuId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuModel> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuModel> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuModel> PublishAsync(MenuId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuModel> DraftAsync(MenuId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyOrders : IOrderDesktopOperations
    {
        public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken) => Task.FromResult(new Page<Order>([], default));
        public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>([]);
        public Task<OrderCatalog> CatalogAsync(CancellationToken cancellationToken) => Task.FromResult(new OrderCatalog([], new Dictionary<DrinkId, Drink>()));
        public Task<Order> PlaceAsync(PlaceOrderRequest request, TagCollection tags, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
