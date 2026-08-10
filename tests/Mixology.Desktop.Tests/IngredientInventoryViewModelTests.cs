using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Desktop.Workspaces;
using Mixology.Desktop.Workspaces.Ingredients;
using Mixology.Desktop.Workspaces.Inventory;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Inventory.Requests;
using Mixology.Presentation.Mutations;
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class IngredientInventoryViewModelTests
{
    [Fact]
    public async Task IngredientRefreshRejectsStalePageAndKeepsTypedSelection()
    {
        Ingredient first = Ingredient("First");
        Ingredient current = Ingredient("Current");
        TaskCompletionSource<Page<Ingredient>> stale = Source<Page<Ingredient>>();
        TaskCompletionSource<Page<Ingredient>> latest = Source<Page<Ingredient>>();
        FakeIngredients operations = new([current])
        {
            List = new Queue<Task<Page<Ingredient>>>([stale.Task, latest.Task]),
        };
        await using IngredientsViewModel viewModel = new(operations);

        Task oldRefresh = viewModel.RefreshAsync();
        Task newRefresh = viewModel.RefreshAsync();
        latest.SetResult(new Page<Ingredient>([current], default));
        await newRefresh;
        stale.SetResult(new Page<Ingredient>([first], default));
        await oldRefresh;
        await UntilAsync(() => viewModel.SelectedIngredient?.Id == current.Id);

        Assert.Equal(current.Id, Assert.Single(viewModel.Items).Id);
        Assert.Equal(current.Id, viewModel.SelectedItem?.Id);
    }

    [Fact]
    public async Task IngredientEditorTracksDirtyStateAndValidatesRetirementRatio()
    {
        Ingredient gin = Ingredient("Gin");
        Ingredient vodka = Ingredient("Vodka");
        FakeIngredients operations = new([gin, vodka]);
        await using IngredientsViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await UntilAsync(() => viewModel.SelectedIngredient is not null && viewModel.CanEdit && viewModel.CanRetire);

        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditorName = "Dry Gin";
        Assert.True(viewModel.IsDirty);
        viewModel.CancelEditorCommand.Execute(null);
        Assert.False(viewModel.IsDirty);

        viewModel.BeginRetireCommand.Execute(null);
        viewModel.ReplacementIngredientId = vodka.Id.Value;
        viewModel.ReplacementRatio = "-1";
        await viewModel.SubmitAsync();
        Assert.IsType<InvalidError>(viewModel.Error);
        Assert.Equal(0, operations.RetireCalls);

        viewModel.ReplacementRatio = "0.75";
        await viewModel.SubmitAsync();
        Assert.Equal(1, operations.RetireCalls);
        Assert.Equal(0.75, operations.LastRetirement?.Retirement.Ratio);
    }

    [Fact]
    public async Task IngredientCreateGatesDuplicateSubmitAndDistinguishesOmittedFromEmptyTags()
    {
        Ingredient created = Ingredient("Created");
        TaskCompletionSource<Ingredient> completion = Source<Ingredient>();
        FakeIngredients operations = new([]) { Create = (_, _, _) => completion.Task };
        await using IngredientsViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        viewModel.BeginCreateCommand.Execute(null);
        viewModel.EditorName = "Created";
        viewModel.EditorCategory = "spirit";
        viewModel.EditorUnit = "oz";
        viewModel.ReplaceTags = true;
        viewModel.EditorTags = string.Empty;

        Task first = viewModel.SubmitAsync();
        Task duplicate = viewModel.SubmitAsync();
        await duplicate;
        Assert.Equal(1, operations.CreateCalls);
        Assert.NotNull(operations.LastTags);
        Assert.Empty(operations.LastTags!);
        completion.SetResult(created);
        await first;
    }

    [Fact]
    public async Task InventoryCostOnlyAdjustmentAcceptsNonUsdAndGatesDuplicateSubmit()
    {
        InventoryListItemViewModel row = InventoryRow("Gin", 12, 2);
        TaskCompletionSource<InventoryStock> completion = Source<InventoryStock>();
        FakeInventory operations = new([row]) { Adjust = (_, _, _) => completion.Task };
        await using InventoryViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await UntilAsync(() => viewModel.SelectedInventory is not null && viewModel.CanAdjust);
        viewModel.BeginAdjustCommand.Execute(null);
        viewModel.EditorDelta = string.Empty;
        viewModel.EditorCost = "EUR 1.25";
        viewModel.EditorReason = "corrected";
        viewModel.ReplaceTags = true;
        viewModel.EditorTags = "region=west";

        Task first = viewModel.SubmitAsync();
        Task duplicate = viewModel.SubmitAsync();
        await duplicate;
        Assert.Equal(1, operations.AdjustCalls);
        Assert.Null(operations.LastAdjustment?.Delta);
        Assert.Equal(Currency.Eur, operations.LastAdjustment?.UnitCost?.Currency);
        Assert.Equal("region=west", operations.LastTags?.Format());
        completion.SetResult(row.Stock with { UnitCost = new Price(1.25m, Currency.Eur) });
        await first;
    }

    [Fact]
    public async Task ErrorsKeepTypedIdentityAndUnknownCausesBecomeSafeInternalErrors()
    {
        InvalidError typed = AppError.Invalid("bad stock filter");
        await using InventoryViewModel typedViewModel = new(new FakeInventory([])
        {
            ListError = typed,
        });
        await typedViewModel.ActivateAsync();
        Assert.Same(typed, typedViewModel.Error);
        Assert.Equal("bad stock filter", typedViewModel.StatusMessage);

        IOException cause = new("secret database path");
        await using IngredientsViewModel safeViewModel = new(new FakeIngredients([])
        {
            ListError = cause,
        });
        await safeViewModel.ActivateAsync();
        InternalError safe = Assert.IsType<InternalError>(safeViewModel.Error);
        Assert.Same(cause, safe.InnerException);
        Assert.Equal("internal error", safeViewModel.StatusMessage);
    }

    [Fact]
    public async Task ActionProjectionHidesUnauthorizedControlsAndExplainsVisibleDisabledControls()
    {
        Ingredient ingredient = Ingredient("Gin");
        FakeIngredients operations = new([ingredient])
        {
            Actions =
            [
                new(IngredientActionProjector.ListAction, true, true),
                new(IngredientActionProjector.CreateAction, false, false),
                new(IngredientActionProjector.EditAction, true, false, "Locked by workflow"),
            ],
        };
        await using IngredientsViewModel viewModel = new(operations);

        await viewModel.ActivateAsync();
        await UntilAsync(() => viewModel.IsEditVisible);

        Assert.False(viewModel.IsCreateVisible);
        Assert.True(viewModel.IsEditVisible);
        Assert.False(viewModel.CanEdit);
        Assert.Equal("Locked by workflow", viewModel.EditDisabledReason);
    }

    [Fact]
    public async Task DisposalCancelsAndDrainsOutstandingDetail()
    {
        Ingredient gin = Ingredient("Gin");
        TaskCompletionSource started = Source();
        bool cancelled = false;
        FakeIngredients operations = new([gin])
        {
            Get = async (_, token) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return gin;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    throw;
                }
            },
        };
        IngredientsViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await started.Task;

        await viewModel.DisposeAsync();

        Assert.True(cancelled);
    }

    [Fact]
    public async Task RealSqliteCedarFactoriesApplyAtomicTaggedMutations()
    {
        await using ProductionFixture fixture = await ProductionFixture.CreateAsync();
        MixologySession session = fixture.Session(Actor.Manager);
        IngredientsModule ingredients = fixture.Get<IngredientsModule>();
        Func<IDesktopWorkspace> ingredientFactory = IngredientsViewModel.CreateFactory(
            ingredients,
            fixture.Get<IngredientActionProjector>(),
            fixture.Get<TaggedMutationCoordinator>(),
            session,
            Actor.Manager);
        await using IngredientsViewModel ingredientsViewModel = Assert.IsType<IngredientsViewModel>(ingredientFactory());
        await ingredientsViewModel.ActivateAsync();
        ingredientsViewModel.BeginCreateCommand.Execute(null);
        ingredientsViewModel.EditorName = "Desktop Gin";
        ingredientsViewModel.EditorCategory = "spirit";
        ingredientsViewModel.EditorUnit = "oz";
        ingredientsViewModel.ReplaceTags = true;
        ingredientsViewModel.EditorTags = "channel=desktop";
        await ingredientsViewModel.SubmitAsync();

        Ingredient ingredient = Assert.Single((await ingredients.ListAsync(session, new ListIngredientsRequest())).Items);
        Assert.Equal(["channel=desktop"], ingredient.Tags.Strings());

        InventoryModule inventory = fixture.Get<InventoryModule>();
        _ = await inventory.SetAsync(session, new SetInventoryRequest(
            ingredient.Id,
            Amount.Create(10, Unit.Ounce),
            new Price(1m, Currency.Usd)));
        Func<IDesktopWorkspace> inventoryFactory = InventoryViewModel.CreateFactory(
            inventory,
            ingredients,
            fixture.Get<InventoryActionProjector>(),
            fixture.Get<TaggedMutationCoordinator>(),
            session,
            Actor.Manager);
        await using InventoryViewModel inventoryViewModel = Assert.IsType<InventoryViewModel>(inventoryFactory());
        await inventoryViewModel.ActivateAsync();
        await UntilAsync(() => inventoryViewModel.SelectedInventory is not null && inventoryViewModel.CanAdjust);
        Assert.True(inventoryViewModel.CanAdjust, inventoryViewModel.AdjustDisabledReason);
        inventoryViewModel.BeginAdjustCommand.Execute(null);
        Assert.Equal(InventoryEditorMode.Adjust, inventoryViewModel.EditorMode);
        inventoryViewModel.EditorDelta = "-2.5";
        inventoryViewModel.EditorCost = "EUR 2.50";
        inventoryViewModel.EditorReason = "used";
        inventoryViewModel.ReplaceTags = true;
        inventoryViewModel.EditorTags = "status=tracked";
        await inventoryViewModel.SubmitAsync();
        Assert.Null(inventoryViewModel.Error);

        InventoryStock stock = await inventory.GetAsync(session, ingredient.Id);
        Assert.Equal(7.5, stock.OnHand.Value, 6);
        Assert.Equal(new Price(2.5m, Currency.Eur), stock.UnitCost);
        Assert.Equal(["status=tracked"], stock.Tags.Strings());
    }

    private static Ingredient Ingredient(string name) => new(
        IngredientId.New(), name, IngredientCategory.Spirit, Unit.Ounce, string.Empty, null, TagCollection.Empty);

    private static InventoryListItemViewModel InventoryRow(string name, double onHand, double reserved)
    {
        Ingredient ingredient = Ingredient(name);
        InventoryStock stock = new(
            InventoryId.New(), ingredient.Id,
            Amount.Create(onHand, Unit.Ounce), Amount.Create(reserved, Unit.Ounce),
            new Price(1m, Currency.Usd), DateTimeOffset.UtcNow, TagCollection.Empty);
        return new(stock, ingredient);
    }

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

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource Source() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task UntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FakeIngredients(IReadOnlyList<Ingredient> rows) : IIngredientsDesktopOperations
    {
        public Queue<Task<Page<Ingredient>>>? List { get; init; }
        public Exception? ListError { get; init; }
        public IReadOnlyList<ActionState> Actions { get; init; } = IngredientActions();
        public Func<IngredientId, CancellationToken, Task<Ingredient>>? Get { get; init; }
        public Func<CreateIngredientRequest, TagCollection?, CancellationToken, Task<Ingredient>>? Create { get; init; }
        public int CreateCalls { get; private set; }
        public int RetireCalls { get; private set; }
        public RetireIngredientRequest? LastRetirement { get; private set; }
        public TagCollection? LastTags { get; private set; }

        public Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken token)
        {
            _ = request;
            _ = token;
            return ListError is not null
                ? Task.FromException<Page<Ingredient>>(ListError)
                : List?.Dequeue() ?? Task.FromResult(new Page<Ingredient>(rows, default));
        }

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken token) =>
            Get?.Invoke(id, token) ?? Task.FromResult(rows.Single(row => row.Id == id));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(Ingredient? selected, CancellationToken token)
        {
            _ = selected;
            _ = token;
            return Task.FromResult(Actions);
        }

        public Task<Ingredient> CreateAsync(CreateIngredientRequest request, TagCollection? tags, CancellationToken token)
        {
            CreateCalls++;
            LastTags = tags;
            return Create?.Invoke(request, tags, token) ?? Task.FromResult(rows[0]);
        }

        public Task<Ingredient> UpdateAsync(UpdateIngredientRequest request, TagCollection? tags, CancellationToken token)
        {
            _ = request;
            _ = tags;
            _ = token;
            return Task.FromResult(rows[0]);
        }

        public Task<Ingredient> RetireAsync(RetireIngredientRequest request, CancellationToken token)
        {
            RetireCalls++;
            LastRetirement = request;
            _ = token;
            return Task.FromResult(rows[0]);
        }
    }

    private sealed class FakeInventory(IReadOnlyList<InventoryListItemViewModel> rows) : IInventoryDesktopOperations
    {
        public Exception? ListError { get; init; }
        public Func<AdjustInventoryRequest, TagCollection?, CancellationToken, Task<InventoryStock>>? Adjust { get; init; }
        public int AdjustCalls { get; private set; }
        public AdjustInventoryRequest? LastAdjustment { get; private set; }
        public TagCollection? LastTags { get; private set; }

        public Task<Page<InventoryListItemViewModel>> ListAsync(ListInventoryRequest request, CancellationToken token)
        {
            _ = request;
            _ = token;
            return ListError is not null
                ? Task.FromException<Page<InventoryListItemViewModel>>(ListError)
                : Task.FromResult(new Page<InventoryListItemViewModel>(rows, default));
        }

        public Task<InventoryListItemViewModel> GetAsync(IngredientId id, CancellationToken token)
        {
            _ = token;
            return Task.FromResult(rows.Single(row => row.Stock.IngredientId == id));
        }

        public Task<IReadOnlyList<ActionState>> ProjectAsync(InventoryStock? selected, CancellationToken token)
        {
            _ = selected;
            _ = token;
            return Task.FromResult(InventoryActions());
        }

        public Task<InventoryStock> AdjustAsync(AdjustInventoryRequest request, TagCollection? tags, CancellationToken token)
        {
            AdjustCalls++;
            LastAdjustment = request;
            LastTags = tags;
            return Adjust?.Invoke(request, tags, token) ?? Task.FromResult(rows[0].Stock);
        }

        public Task<InventoryStock> SetAsync(SetInventoryRequest request, TagCollection? tags, CancellationToken token)
        {
            _ = request;
            _ = tags;
            _ = token;
            return Task.FromResult(rows[0].Stock);
        }
    }

    private sealed class ProductionFixture : IAsyncDisposable
    {
        private readonly string root;
        private readonly DesktopHost host;

        private ProductionFixture(string root, DesktopHost host)
        {
            this.root = root;
            this.host = host;
        }

        public static async Task<ProductionFixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "mixology-desktop-stock", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DesktopHost host = await DesktopHost.OpenAsync(
                new DesktopOptions(Path.Combine(root, "mixology.db"), Actor.Manager));
            return new(root, host);
        }

        public T Get<T>() where T : notnull => host.Services.GetRequiredService<T>();
        public MixologySession Session(Actor actor) => Get<MixologySessionFactory>().Create(actor);

        public async ValueTask DisposeAsync()
        {
            await host.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
