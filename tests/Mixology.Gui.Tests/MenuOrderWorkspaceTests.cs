using Mixology.Application.Presentation.Actions;
using Mixology.Gui.Workspaces.Menus;
using Mixology.Gui.Workspaces.Orders;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Orders.Requests;
using Xunit;

namespace Mixology.Gui.Tests;

public sealed class MenuOrderWorkspaceTests
{
    [Fact]
    public async Task MenusKeepSelectionAndExposeIndependentLifecycleActions()
    {
        Menu first = CreateMenu("First");
        Menu second = CreateMenu("Second");
        MenuOperations operations = new([first, second]);
        await using MenusViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();
        viewModel.Selected = viewModel.Rows[1];
        await viewModel.DrainAsync();

        Assert.Equal(second.Id, viewModel.Detail?.Id);
        Assert.True(viewModel.CanEdit);
        Assert.True(viewModel.CanPublish);
        Assert.True(viewModel.CanAnalyze);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.DrainAsync();
        Assert.Equal(second.Id.Value, viewModel.Selected?.Id);
    }

    [Fact]
    public async Task MenuFormsAreDirtyAndInvalidInputRemainsTyped()
    {
        MenuOperations operations = new([]);
        await using MenusViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();

        viewModel.StartCreateCommand.Execute(null);
        viewModel.Name = "Evening";
        Assert.True(viewModel.IsDirty);

        viewModel.Tags = "=broken";
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.IsType<InvalidError>(viewModel.Error);
    }

    [Fact]
    public async Task MenuUnknownFailuresAreNormalizedWithTheirCause()
    {
        InvalidOperationException cause = new("database secret");
        MenuOperations operations = new([]) { ListError = cause };
        await using MenusViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();

        InternalError error = Assert.IsType<InternalError>(viewModel.Error);
        Assert.Same(cause, error.InnerException);
        Assert.Equal("internal error", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SupersededMenuPageCannotReplaceTheCurrentGeneration()
    {
        TaskCompletionSource<Page<Menu>> stale = Source<Page<Menu>>();
        TaskCompletionSource<Page<Menu>> current = Source<Page<Menu>>();
        int calls = 0;
        MenuOperations operations = new([])
        {
            ListHandler = (_, _) => ++calls == 1 ? stale.Task : current.Task,
        };
        await using MenusViewModel viewModel = new(operations);

        Task first = viewModel.ActivateAsync();
        Task second = viewModel.RefreshCommand.ExecuteAsync(null);
        Menu expected = CreateMenu("Current");
        current.SetResult(new Page<Menu>([expected], default));
        await second;
        stale.SetResult(new Page<Menu>([CreateMenu("Stale")], default));
        await first;

        Assert.Equal(expected.Id.Value, Assert.Single(viewModel.Rows).Id);
    }

    [Fact]
    public async Task OrderPlaceFormTracksDirtyStateAndSubmitsTypedLines()
    {
        Menu menu = CreateMenu("Published") with { Status = MenuStatus.Published };
        OrderOperations operations = new([], new OrderCatalog([menu], new Dictionary<DrinkId, Drink>()));
        await using OrdersViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();
        viewModel.StartPlaceCommand.Execute(null);
        viewModel.PlaceMenu = Assert.Single(viewModel.Menus);
        viewModel.PlaceNotes = "No garnish";

        Assert.True(viewModel.IsDirty);
        await viewModel.PlaceCommand.ExecuteAsync(null);
        Assert.IsType<InvalidError>(viewModel.Error);
        Assert.Equal("order must have at least one item", viewModel.StatusMessage);
    }

    [Fact]
    public async Task OrdersKeepStableSelectionAndProjectTerminalActionsIndependently()
    {
        Menu menu = CreateMenu("Service") with { Status = MenuStatus.Published };
        Order pending = CreateOrder(menu.Id, OrderStatus.Pending);
        OrderOperations operations = new([pending], new OrderCatalog([menu], new Dictionary<DrinkId, Drink>()));
        await using OrdersViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();
        viewModel.Selected = Assert.Single(viewModel.Rows);
        await viewModel.DrainAsync();

        Assert.Equal(pending.Id, viewModel.Detail?.Id);
        Assert.True(viewModel.CanComplete);
        Assert.True(viewModel.CanCancel);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.DrainAsync();
        Assert.Equal(pending.Id.Value, viewModel.Selected?.Id);
    }

    private static Menu CreateMenu(string name) => new(
        MenuId.New(),
        name,
        "description",
        [],
        MenuStatus.Draft,
        DateTimeOffset.Parse("2026-08-09T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        null,
        null,
        TagCollection.Empty);

    private static Order CreateOrder(MenuId menu, OrderStatus status) => new(
        OrderId.New(),
        menu,
        [new(DrinkId.New(), 1, string.Empty)],
        [],
        [],
        status,
        DateTimeOffset.Parse("2026-08-09T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        null,
        "notes",
        null,
        TagCollection.Empty);

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class MenuOperations(IReadOnlyList<Menu> menus) : IMenuDesktopOperations
    {
        public Exception? ListError { get; init; }
        public Func<ListMenusRequest, CancellationToken, Task<Page<Menu>>>? ListHandler { get; init; }
        public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken) =>
            ListError is not null
                ? Task.FromException<Page<Menu>>(ListError)
                : ListHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(new Page<Menu>(menus, default));
        public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken) => Task.FromResult(menus.Single(value => value.Id == id));
        public Task<IReadOnlyList<ActionState>> ProjectAsync(Menu? selected, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>(
        [
            new(MenuActionProjector.ListAction, true, true), new(MenuActionProjector.CreateAction, true, true),
            new(MenuActionProjector.EditAction, true, true), new(MenuActionProjector.DeleteAction, true, true),
            new(MenuActionProjector.AddDrinkAction, true, true), new(MenuActionProjector.RemoveDrinkAction, true, true),
            new(MenuActionProjector.PublishAction, true, true), new(MenuActionProjector.DraftAction, true, true),
            new(MenuActionProjector.ReadinessAction, true, true),
        ]);
        public Task<IReadOnlyList<Drink>> DrinksAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Drink>>([]);
        public Task<Menu> CreateAsync(CreateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken) => Task.FromResult(CreateMenu(request.Name) with { Tags = tags ?? TagCollection.Empty });
        public Task<Menu> UpdateAsync(UpdateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken) => Task.FromResult(menus.Single(value => value.Id == request.Id) with { Name = request.Name, Tags = tags ?? TagCollection.Empty });
        public Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken) => GetAsync(id, cancellationToken);
        public Task<Menu> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken) => GetAsync(request.MenuId, cancellationToken);
        public Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken) => GetAsync(request.MenuId, cancellationToken);
        public async Task<Menu> PublishAsync(MenuId id, CancellationToken cancellationToken) => (await GetAsync(id, cancellationToken)) with { Status = MenuStatus.Published };
        public async Task<Menu> DraftAsync(MenuId id, CancellationToken cancellationToken) => (await GetAsync(id, cancellationToken)) with { Status = MenuStatus.Draft };
        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken) => Task.FromResult(new ReadinessReport(id, MenuStatus.Draft, []));
        public async Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken) => new(await GetAsync(id, cancellationToken), [], 0, 0, null);
    }

    private sealed class OrderOperations(IReadOnlyList<Order> orders, OrderCatalog catalog) : IOrderDesktopOperations
    {
        public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken) => Task.FromResult(new Page<Order>(orders, default));
        public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken) => Task.FromResult(orders.Single(value => value.Id == id));
        public Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActionState>>(
        [
            new(OrderActionProjector.ListAction, true, true), new(OrderActionProjector.PlaceAction, true, true),
            new(OrderActionProjector.CompleteAction, true, true), new(OrderActionProjector.CancelAction, true, true),
        ]);
        public Task<OrderCatalog> CatalogAsync(CancellationToken cancellationToken) => Task.FromResult(catalog);
        public Task<Order> PlaceAsync(PlaceOrderRequest request, TagCollection tags, CancellationToken cancellationToken) => Task.FromResult(CreateOrder(request.MenuId, OrderStatus.Pending) with { Tags = tags });
        public async Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken) => (await GetAsync(id, cancellationToken)) with { Status = OrderStatus.Completed, CompletedAt = DateTimeOffset.UtcNow };
        public async Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken) => (await GetAsync(id, cancellationToken)) with { Status = OrderStatus.Cancelled };
    }
}
