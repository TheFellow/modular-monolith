using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Orders.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Mixology.Tui.Workspaces.Menus;
using Mixology.Tui.Workspaces.Orders;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class MenuOrderWorkspaceTests
{
    [Fact]
    public async Task MenuDetailRejectsStaleReadinessAndPublishIsIndependentFromEdit()
    {
        Menu first = MenuValue("First");
        Menu second = MenuValue("Second") with
        {
            Items = [new MenuItem(DrinkId.New(), "Fizz", null, false, Availability.Available, 0)],
        };
        TaskCompletionSource<Menu> firstDetail = Source<Menu>();
        TaskCompletionSource<Menu> secondDetail = Source<Menu>();
        FakeMenus operations = new([first, second])
        {
            Get = (id, _) => id == first.Id ? firstDetail.Task : secondDetail.Task,
            Actions = selected => MenuActions(selected, edit: false, publish: true),
        };
        await using MenusWorkspace workspace = new(operations);

        await workspace.ActivateAsync();
        _ = workspace.Handle('j');
        secondDetail.SetResult(second);
        await UntilAsync(() => workspace.Render(new Viewport(80, 21)).Contains("Second", StringComparison.Ordinal));
        firstDetail.SetResult(first);
        await workspace.DrainAsync();

        string rendered = workspace.Render(new Viewport(80, 21));
        Assert.Contains("[p] publish", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("[e] edit", rendered, StringComparison.Ordinal);
        Assert.True(workspace.Handle('p'));
        Assert.Equal(MenusWorkspaceMode.Publish, workspace.Mode);
        Assert.True(rendered.Split('\n').Length <= 21);
        Assert.All(rendered.Split('\n'), static line => Assert.True(line.Length <= 80));
    }

    [Fact]
    public async Task MenuKeyboardPickerFiltersSelectsAndGatesDuplicateSubmission()
    {
        DrinkId gin = DrinkId.New();
        Menu menu = MenuValue("Late Night");
        TaskCompletionSource<Menu> completion = Source<Menu>();
        FakeMenus operations = new([menu])
        {
            Catalog = [new MenuDrinkOption(gin, "Gin Fizz"), new MenuDrinkOption(DrinkId.New(), "Old Fashioned")],
            Add = (_, _, _) => completion.Task,
        };
        await using MenusWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        Assert.True(workspace.Handle('a'));
        await workspace.DrainAsync();
        foreach (char key in "Fizz") { _ = workspace.Handle(key); }
        Assert.Contains("Gin Fizz", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
        _ = workspace.Handle('\r');
        _ = workspace.Handle(MenusWorkspace.SubmitKey);

        Assert.Equal(1, operations.AddCalls);
        Assert.Equal(MenusWorkspaceMode.Submitting, workspace.Mode);
        completion.SetResult(menu with
        {
            Items = [new MenuItem(gin, "Gin Fizz", null, false, Availability.Available, 0)],
        });
        await workspace.DrainAsync();
        Assert.Equal(MenusWorkspaceMode.Browse, workspace.Mode);
    }

    [Fact]
    public async Task MenuReadinessAndUnknownCostAnalysisRemainExplicit()
    {
        DrinkId drink = DrinkId.New();
        Menu menu = MenuValue("Blocked") with
        {
            Items = [new MenuItem(drink, "Mystery", null, false, Availability.Unavailable, 0)],
        };
        ReadinessReport report = new(menu.Id, menu.Status,
        [
            new ReadinessFinding(
                ReadinessSeverity.Blocker,
                ReadinessCode.Unavailable,
                drink,
                null,
                "drink is unavailable"),
            new ReadinessFinding(
                ReadinessSeverity.Warning,
                ReadinessCode.LowStock,
                drink,
                null,
                "stock is low"),
        ]);
        FakeMenus operations = new([menu])
        {
            Readiness = report,
            Analysis = new MenuAnalysis(
                menu,
                [new MenuItemAnalysis(drink, "Mystery", Availability.Unavailable, [], null, true, null, null, null)],
                0,
                1,
                null),
        };
        await using MenusWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        string detail = workspace.Render(new Viewport(80, 21));
        Assert.Contains("1 blocker(s), 1 warning(s)", detail, StringComparison.Ordinal);
        Assert.Contains("blocker · unavailable", detail, StringComparison.Ordinal);
        Assert.Contains("warning · low_stock", detail, StringComparison.Ordinal);
        Assert.Contains("Publish: Resolve menu readiness blocke", detail, StringComparison.Ordinal);
        _ = workspace.Handle('y');
        _ = workspace.Handle(MenusWorkspace.SubmitKey);
        await workspace.DrainAsync();
        Assert.Contains("cost unknown", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidNumericInputsStayInTheirFormsWithoutStartingOperations()
    {
        Menu menu = MenuValue("Numbers") with
        {
            Items = [new MenuItem(DrinkId.New(), "Fizz", null, false, Availability.Available, 0)],
        };
        FakeMenus operations = new([menu]);
        await using MenusWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('f');
        workspace.SetField("Page size", "0");
        _ = workspace.Handle(MenusWorkspace.SubmitKey);
        Assert.Equal(MenusWorkspaceMode.Filter, workspace.Mode);
        Assert.Contains("greater than zero", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
        _ = workspace.Handle('\u001b');

        _ = workspace.Handle('y');
        workspace.SetField("Target margin", "NaN");
        _ = workspace.Handle(MenusWorkspace.SubmitKey);
        Assert.Equal(MenusWorkspaceMode.Analyze, workspace.Mode);
        Assert.Contains("between 0 and 1", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
        Assert.Equal(0, operations.AnalyzeCalls);
    }

    [Fact]
    public void OrderPlacementUsesKeyboardPickersCombinesLinesAndKeepsNotes()
    {
        OrderDrinkOption gin = new(DrinkId.New(), "Gin Fizz");
        OrderPlacementEditor editor = new();
        editor.SetCatalog([new OrderMenuOption(MenuId.New(), "Late Night", [gin])]);

        foreach (char key in "Night") { editor.Handle(key); }
        editor.Handle('\r');
        foreach (char key in "Fizz") { editor.Handle(key); }
        editor.Handle('\t');
        editor.SetField(OrderPlacementField.Quantity, "2");
        editor.Handle('\t');
        editor.SetField(OrderPlacementField.ItemNotes, "first, stirred");
        editor.FieldShouldBe(OrderPlacementField.ItemNotes);
        editor.SetField(OrderPlacementField.Drink, "Fizz");
        editor.ChooseDrinkField();
        editor.AddSelectedDrink();
        editor.SetField(OrderPlacementField.Quantity, "3");
        editor.SetField(OrderPlacementField.ItemNotes, "second, stirred");
        editor.AddSelectedDrink();
        editor.SetField(OrderPlacementField.OrderNotes, "patio\nVIP");

        PlaceOrderRequest request = editor.Build();
        PlaceOrderItem item = Assert.Single(request.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal("second, stirred", item.Notes);
        Assert.Equal("patio\nVIP", request.Notes);
    }

    [Fact]
    public async Task DeferredOrderRefreshRejectsStaleCompletionAndDisposalDrainsCancellation()
    {
        Order old = OrderValue(OrderStatus.Pending);
        Order current = OrderValue(OrderStatus.Blocked);
        TaskCompletionSource<Page<Order>> first = Source<Page<Order>>();
        TaskCompletionSource<Page<Order>> second = Source<Page<Order>>();
        int calls = 0;
        FakeOrders operations = new([])
        {
            List = (_, _) => Interlocked.Increment(ref calls) == 1 ? first.Task : second.Task,
        };
        OrdersWorkspace workspace = new(operations);
        Task oldLoad = workspace.ActivateAsync();
        Task currentLoad = workspace.RefreshAsync();
        second.SetResult(new Page<Order>([current], default));
        await currentLoad;
        first.SetResult(new Page<Order>([old], default));
        await oldLoad;
        await workspace.DrainAsync();
        Assert.Equal(current.Id, Assert.Single(workspace.Rows).Id);
        await workspace.DisposeAsync();

        TaskCompletionSource started = Source();
        bool cancelled = false;
        FakeOrders pending = new([])
        {
            List = async (_, token) =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return new Page<Order>([], default);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    throw;
                }
            },
        };
        OrdersWorkspace cancellable = new(pending);
        _ = cancellable.ActivateAsync();
        await started.Task;
        await cancellable.DisposeAsync();
        Assert.True(cancelled);
    }

    [Fact]
    public async Task RealSqliteCedarWorkspacesDriveReservationAndAvailabilityLifecycle()
    {
        await using ProductionFixture fixture = await ProductionFixture.CreateAsync();
        MixologySession session = fixture.Session(Actor.Manager);
        IngredientsModule ingredients = fixture.Get<IngredientsModule>();
        InventoryModule inventory = fixture.Get<InventoryModule>();
        DrinksModule drinks = fixture.Get<DrinksModule>();
        MenusModule menus = fixture.Get<MenusModule>();
        OrdersModule orders = fixture.Get<OrdersModule>();
        Ingredient ingredient = await ingredients.CreateAsync(
            session,
            new CreateIngredientRequest("Lifecycle Gin", IngredientCategory.Spirit, Unit.Ounce));
        _ = await inventory.SetAsync(
            session,
            new SetInventoryRequest(
                ingredient.Id,
                Amount.Create(3, Unit.Ounce),
                new Price(1m, Currency.Usd)));
        Drink drink = await drinks.CreateAsync(
            session,
            new CreateDrinkRequest(
                "Lifecycle Fizz",
                DrinkCategory.Cocktail,
                GlassType.Coupe,
                new Recipe([new RecipeIngredient(ingredient.Id, Amount.Create(1, Unit.Ounce))], ["Stir"])));
        Menu menu = await menus.CreateAsync(session, new CreateMenuRequest("Lifecycle Menu"));
        menu = await menus.AddDrinkAsync(session, new AddMenuItemRequest(menu.Id, drink.Id));
        _ = await menus.PublishAsync(session, menu.Id);

        Func<ITuiWorkspace> orderFactory = OrdersWorkspace.CreateFactory(
            orders,
            menus,
            drinks,
            fixture.Get<OrderActionProjector>(),
            fixture.Get<TaggedMutationCoordinator>(),
            session,
            Actor.Manager);
        await using OrdersWorkspace workspace = Assert.IsType<OrdersWorkspace>(orderFactory());
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('c');
        await workspace.DrainAsync();
        workspace.Placement!.ChooseMenu();
        workspace.Placement.AddSelectedDrink();
        _ = workspace.Handle(OrdersWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Order first = Assert.Single((await orders.ListAsync(session, new ListOrdersRequest())).Items);
        InventoryStock reserved = await inventory.GetAsync(session, ingredient.Id);
        Assert.Equal(1, reserved.Reserved.Value, 6);
        Assert.Equal(Availability.Limited, Assert.Single((await menus.GetAsync(session, menu.Id)).Items).Availability);

        _ = workspace.Handle('x');
        _ = workspace.Handle('\r');
        await workspace.DrainAsync();
        Assert.Equal(OrderStatus.Cancelled, (await orders.GetAsync(session, first.Id)).Status);
        Assert.Equal(0, (await inventory.GetAsync(session, ingredient.Id)).Reserved.Value, 6);
        Assert.Equal(Availability.Available, Assert.Single((await menus.GetAsync(session, menu.Id)).Items).Availability);

        _ = workspace.Handle('c');
        await workspace.DrainAsync();
        workspace.Placement!.ChooseMenu();
        workspace.Placement.AddSelectedDrink();
        _ = workspace.Handle(OrdersWorkspace.SubmitKey);
        await workspace.DrainAsync();
        _ = workspace.Handle('o');
        _ = workspace.Handle('\r');
        await workspace.DrainAsync();

        Order completed = Assert.Single(
            (await orders.ListAsync(session, new ListOrdersRequest(OrderStatus.Completed))).Items);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        InventoryStock consumed = await inventory.GetAsync(session, ingredient.Id);
        Assert.Equal(2, consumed.OnHand.Value, 6);
        Assert.Equal(0, consumed.Reserved.Value, 6);
        Assert.Equal(Availability.Limited, Assert.Single((await menus.GetAsync(session, menu.Id)).Items).Availability);
    }

    private static Menu MenuValue(string name) => new(
        MenuId.New(),
        name,
        string.Empty,
        [],
        MenuStatus.Draft,
        new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
        null,
        null,
        TagCollection.Empty);

    private static Order OrderValue(OrderStatus status) => new(
        OrderId.New(),
        MenuId.New(),
        [new OrderItem(DrinkId.New(), 1, string.Empty)],
        [],
        [],
        status,
        new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
        status == OrderStatus.Completed
            ? new DateTimeOffset(2026, 8, 9, 12, 5, 0, TimeSpan.Zero)
            : null,
        string.Empty,
        null,
        TagCollection.Empty);

    private static List<ActionState> MenuActions(
        Menu? selected,
        bool edit = true,
        bool publish = true)
    {
        List<ActionState> states =
        [
            new(MenuActionProjector.ListAction, true, true),
            new(MenuActionProjector.CreateAction, true, true),
        ];
        if (selected is null) { return states; }
        states.AddRange(
        [
            new(MenuActionProjector.EditAction, true, edit, edit ? string.Empty : "edit denied"),
            new(MenuActionProjector.DeleteAction, true, true),
            new(MenuActionProjector.AddDrinkAction, true, true),
            new(MenuActionProjector.RemoveDrinkAction, true, selected.Items.Count > 0),
            new(MenuActionProjector.PublishAction, true, publish),
            new(MenuActionProjector.DraftAction, true, selected.Status == MenuStatus.Published),
            new(MenuActionProjector.ReadinessAction, true, true),
        ]);
        return states;
    }

    private static List<ActionState> OrderActions(Order? selected)
    {
        List<ActionState> states =
        [
            new(OrderActionProjector.ListAction, true, true),
            new(OrderActionProjector.PlaceAction, true, true),
        ];
        if (selected is null) { return states; }
        states.Add(new ActionState(
            OrderActionProjector.CompleteAction,
            true,
            selected.Status == OrderStatus.Pending,
            selected.Status == OrderStatus.Blocked ? "Reserved stock is short." : string.Empty));
        states.Add(new ActionState(
            OrderActionProjector.CancelAction,
            true,
            selected.Status is var status && (status == OrderStatus.Pending || status == OrderStatus.Blocked)));
        return states;
    }

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource Source() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task UntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition()) { await Task.Delay(10, timeout.Token); }
    }

    private sealed class FakeMenus(IReadOnlyList<Menu> rows) : IMenusWorkspaceOperations
    {
        public Func<MenuId, CancellationToken, Task<Menu>>? Get { get; init; }
        public Func<Menu?, IReadOnlyList<ActionState>> Actions { get; init; } = selected => MenuActions(selected);
        public IReadOnlyList<MenuDrinkOption> Catalog { get; init; } = [];
        public ReadinessReport? Readiness { get; init; }
        public MenuAnalysis? Analysis { get; init; }
        public Func<AddMenuItemRequest, TagCollection?, CancellationToken, Task<Menu>>? Add { get; init; }
        public int AddCalls { get; private set; }
        public int AnalyzeCalls { get; private set; }

        public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(new Page<Menu>(rows, default));
        }

        public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken) =>
            Get?.Invoke(id, cancellationToken) ?? Task.FromResult(rows.Single(row => row.Id == id));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(Menu? selected, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(Actions(selected));
        }

        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(Readiness ?? new ReadinessReport(id, rows.Single(row => row.Id == id).Status, []));
        }

        public Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken)
        {
            AnalyzeCalls++;
            _ = id;
            _ = targetMargin;
            _ = cancellationToken;
            return Task.FromResult(Analysis ?? new MenuAnalysis(rows[0], [], 0, 0, null));
        }

        public Task<IReadOnlyList<MenuDrinkOption>> DrinkCatalogAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(Catalog);
        }

        public Task<Menu> CreateAsync(CreateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = request; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Menu> UpdateAsync(UpdateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = request; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken)
        {
            _ = id; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Menu> AddDrinkAsync(AddMenuItemRequest request, TagCollection? tags, CancellationToken cancellationToken)
        {
            AddCalls++;
            return Add?.Invoke(request, tags, cancellationToken) ?? Task.FromResult(rows[0]);
        }

        public Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = request; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Menu> PublishAsync(MenuId id, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = id; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Menu> DraftAsync(MenuId id, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = id; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }
    }

    private sealed class FakeOrders(IReadOnlyList<Order> rows) : IOrdersWorkspaceOperations
    {
        public Func<ListOrdersRequest, CancellationToken, Task<Page<Order>>>? List { get; init; }

        public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken) =>
            List?.Invoke(request, cancellationToken) ?? Task.FromResult(new Page<Order>(rows, default));

        public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(rows.Single(row => row.Id == id));
        }

        public Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<ActionState>>(OrderActions(selected));
        }

        public Task<IReadOnlyList<OrderMenuOption>> CatalogAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<OrderMenuOption>>([]);
        }

        public Task<Order> PlaceAsync(PlaceOrderRequest request, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = request; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Order> CompleteAsync(OrderId id, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = id; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Order> CancelAsync(OrderId id, TagCollection? tags, CancellationToken cancellationToken)
        {
            _ = id; _ = tags; _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }
    }

    private sealed class ProductionFixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly TuiHost host;

        private ProductionFixture(string root, TuiHost host)
        {
            this.root = root;
            this.host = host;
        }

        public static async Task<ProductionFixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-menu-order-tui", Guid.NewGuid().ToString("N"));
            string database = Path.Combine(root, "mixology.db");
            string log = Path.Combine(root, "mixology.log");
            TuiOptions options = TuiOptions.Create(database, "manager", "error", "text", log, metrics: false);
            return new ProductionFixture(root, await TuiHost.OpenAsync(options));
        }

        public T Get<T>() where T : notnull => host.Services.GetRequiredService<T>();
        public MixologySession Session(Actor actor) => Get<MixologySessionFactory>().Create(actor);

        public async ValueTask DisposeAsync()
        {
            await host.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }
}

internal static class OrderPlacementEditorAssertions
{
    public static void FieldShouldBe(this OrderPlacementEditor editor, OrderPlacementField field) =>
        Assert.Equal(field, editor.Field);

    public static void ChooseDrinkField(this OrderPlacementEditor editor)
    {
        while (editor.Field != OrderPlacementField.Drink) { editor.Handle('\t'); }
    }
}
