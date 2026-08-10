using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
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
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Serilog.Events;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class IngredientInventoryWorkspaceTests
{
    [Fact]
    public async Task IngredientDetailRejectsStaleSelectionResponseAndDisposalDrainsIt()
    {
        Ingredient gin = Ingredient("Gin");
        Ingredient lime = Ingredient("Lime", IngredientCategory.Juice);
        TaskCompletionSource<Ingredient> ginDetail = Source<Ingredient>();
        TaskCompletionSource<Ingredient> limeDetail = Source<Ingredient>();
        TaskCompletionSource ginStarted = Source();
        TaskCompletionSource limeStarted = Source();
        FakeIngredients operations = new([gin, lime])
        {
            Get = (id, cancellationToken) =>
            {
                _ = cancellationToken;
                if (id == gin.Id)
                {
                    ginStarted.TrySetResult();
                    return ginDetail.Task;
                }

                limeStarted.TrySetResult();
                return limeDetail.Task;
            },
        };
        IngredientsWorkspace workspace = new(operations);

        await workspace.ActivateAsync();
        await ginStarted.Task;
        Assert.True(workspace.Handle('j'));
        await limeStarted.Task;
        limeDetail.SetResult(lime);
        await UntilAsync(() => workspace.Render(new Viewport(80, 21)).Contains("Category: juice", StringComparison.Ordinal));
        ginDetail.SetResult(gin);
        await workspace.DrainAsync();

        string rendered = workspace.Render(new Viewport(80, 21));
        Assert.Contains("Lime", rendered, StringComparison.Ordinal);
        Assert.Contains("Category: juice", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Category: spirit", rendered, StringComparison.Ordinal);
        await workspace.DisposeAsync();
    }

    [Fact]
    public async Task IngredientCreateOwnsInputAndGatesDuplicateSubmit()
    {
        Ingredient existing = Ingredient("Gin");
        Ingredient created = Ingredient("Vodka");
        TaskCompletionSource<Ingredient> completion = Source<Ingredient>();
        FakeIngredients operations = new([existing]) { Create = (_, _, _) => completion.Task };
        await using IngredientsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        Assert.True(workspace.Handle('c'));
        Assert.Equal(InputOwnership.Edit, workspace.InputOwnership);
        workspace.SetField("Name", "Vodka");
        workspace.SetField("Category", "spirit");
        workspace.SetField("Unit", "oz");
        workspace.SetField("Complete tags (optional)", "audience=sommelier");
        Assert.True(workspace.Handle(IngredientsWorkspace.SubmitKey));
        Assert.True(workspace.Handle(IngredientsWorkspace.SubmitKey));
        Assert.True(workspace.Handle('\u001b'));

        Assert.Equal(1, operations.CreateCalls);
        Assert.Equal(IngredientsWorkspaceMode.Submitting, workspace.Mode);
        completion.SetResult(created);
        await workspace.DrainAsync();
        Assert.Equal(IngredientsWorkspaceMode.Browse, workspace.Mode);
    }

    [Fact]
    public async Task IngredientFilterPagingAndRefreshPreserveStableSelection()
    {
        Ingredient first = Ingredient("First");
        Ingredient second = Ingredient("Second");
        Cursor cursor = new("ing-00000000000000000000000000");
        FakeIngredients operations = new([first, second])
        {
            List = (request, _) => Task.FromResult(request.Cursor.IsEmpty
                ? new Page<Ingredient>([first, second], cursor)
                : new Page<Ingredient>([second], default)),
        };
        await using IngredientsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();
        _ = workspace.Handle('j');
        await workspace.DrainAsync();

        _ = workspace.Handle('r');
        await workspace.DrainAsync();
        Assert.Equal(second.Id, workspace.Selected?.Id);
        _ = workspace.Handle(']');
        await workspace.DrainAsync();
        Assert.Equal(cursor, operations.Requests[^1].Cursor);
        _ = workspace.Handle('[');
        await workspace.DrainAsync();
        Assert.True(operations.Requests[^1].Cursor.IsEmpty);

        _ = workspace.Handle('h');
        string help = workspace.Render(new Viewport(80, 21));
        Assert.Contains("Fields: id, name, category, unit, description, tags", help, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IngredientRetirementRequiresPositiveReplacementRatio()
    {
        Ingredient gin = Ingredient("Gin");
        Ingredient vodka = Ingredient("Vodka");
        FakeIngredients operations = new([gin, vodka]);
        await using IngredientsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('R');
        workspace.SetField("Replacement ingredient ID", vodka.Id.Value);
        workspace.SetField("Replacement ratio", "-1");
        _ = workspace.Handle(IngredientsWorkspace.SubmitKey);

        Assert.Equal(0, operations.RetireCalls);
        Assert.Equal(IngredientsWorkspaceMode.Retire, workspace.Mode);
        Assert.Contains("greater than zero", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);

        workspace.SetField("Replacement ratio", "0.75");
        _ = workspace.Handle(IngredientsWorkspace.SubmitKey);
        await workspace.DrainAsync();
        Assert.Equal(1, operations.RetireCalls);
        Assert.Equal(vodka.Id, operations.LastRetirement?.Retirement.ReplacementId);
        Assert.Equal(0.75, operations.LastRetirement?.Retirement.Ratio);
    }

    [Fact]
    public async Task InventoryUsesAvailableForStatusAndRejectsStaleDetail()
    {
        InventoryWorkspaceRow gin = InventoryRow("Gin", 12, 5);
        InventoryWorkspaceRow lime = InventoryRow("Lime", 20, 0, IngredientCategory.Juice);
        TaskCompletionSource<InventoryWorkspaceRow> ginDetail = Source<InventoryWorkspaceRow>();
        TaskCompletionSource<InventoryWorkspaceRow> limeDetail = Source<InventoryWorkspaceRow>();
        FakeInventory operations = new([gin, lime])
        {
            Get = (id, cancellationToken) =>
            {
                _ = cancellationToken;
                return id == gin.Stock.IngredientId ? ginDetail.Task : limeDetail.Task;
            },
        };
        await using InventoryWorkspace workspace = new(operations);

        await workspace.ActivateAsync();
        Assert.Contains("LOW", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
        _ = workspace.Handle('j');
        limeDetail.SetResult(lime);
        await UntilAsync(() => workspace.Render(new Viewport(80, 21)).Contains("Category: juice", StringComparison.Ordinal));
        ginDetail.SetResult(gin);
        await workspace.DrainAsync();

        string rendered = workspace.Render(new Viewport(80, 21));
        Assert.Contains("Lime", rendered, StringComparison.Ordinal);
        Assert.Contains("Status: OK", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Status: LOW", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InventoryCostOnlyAdjustmentSupportsNonUsdAndGatesDuplicateSubmit()
    {
        InventoryWorkspaceRow gin = InventoryRow("Gin", 12, 0);
        TaskCompletionSource<InventoryStock> completion = Source<InventoryStock>();
        FakeInventory operations = new([gin]) { Adjust = (_, _, _) => completion.Task };
        await using InventoryWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        Assert.True(workspace.Handle('a'));
        workspace.SetField("Cost per unit", "EUR 1.25");
        workspace.SetField("Reason", "corrected");
        workspace.SetField("Complete tags (optional)", "region=west");
        _ = workspace.Handle(InventoryWorkspace.SubmitKey);
        _ = workspace.Handle(InventoryWorkspace.SubmitKey);

        Assert.Equal(1, operations.AdjustCalls);
        Assert.Null(operations.LastAdjustment?.Delta);
        Assert.Equal(Currency.Eur, operations.LastAdjustment?.UnitCost?.Currency);
        Assert.Equal("region=west", operations.LastTags?.Format());
        completion.SetResult(gin.Stock with { UnitCost = new Price(1.25m, Currency.Eur) });
        await workspace.DrainAsync();
    }

    [Fact]
    public async Task InventorySetUsesSelectedUnitAndPreservesCostWhenOptionalFieldIsBlank()
    {
        InventoryWorkspaceRow gin = InventoryRow("Gin", 12, 0);
        FakeInventory operations = new([gin]);
        await using InventoryWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('s');
        workspace.SetField("Quantity", "5.5");
        workspace.SetField("Unit", "ml");
        workspace.SetField("Cost per unit", string.Empty);
        _ = workspace.Handle(InventoryWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Assert.Equal(1, operations.SetCalls);
        Assert.Equal(5.5, operations.LastSet?.OnHand.Value);
        Assert.Equal(Unit.Milliliter, operations.LastSet?.OnHand.Unit);
        Assert.Equal(new Price(1m, Currency.Usd), operations.LastSet?.UnitCost);
        Assert.Null(operations.LastSetTags);
    }

    [Fact]
    public async Task WorkspaceDisposalCancelsAndDrainsOutstandingDetailAndErrorsStaySafe()
    {
        Ingredient gin = Ingredient("Gin");
        TaskCompletionSource started = Source();
        bool cancelled = false;
        FakeIngredients operations = new([gin])
        {
            Get = async (_, cancellationToken) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return gin;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    throw;
                }
            },
        };
        IngredientsWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await started.Task;

        await workspace.DisposeAsync();

        Assert.True(cancelled);

        FakeIngredients broken = new([gin])
        {
            List = (_, _) => Task.FromException<Page<Ingredient>>(new IOException("secret path")),
        };
        await using IngredientsWorkspace safe = new(broken);
        await safe.ActivateAsync();
        Assert.Equal("internal error", safe.Status?.Message);
        Assert.Equal(TerminalErrorStyle.Error, safe.Status?.Style);
    }

    [Fact]
    public async Task RealSqliteCedarIngredientCreateAtomicallyReplacesTags()
    {
        await using ProductionFixture fixture = await ProductionFixture.CreateAsync();
        IngredientsModule module = fixture.Get<IngredientsModule>();
        MixologySession session = fixture.Session(Actor.Manager);
        Func<ITuiWorkspace> factory = IngredientsWorkspace.CreateFactory(
            module,
            fixture.Get<IngredientActionProjector>(),
            fixture.Get<TaggedMutationCoordinator>(),
            session,
            Actor.Manager);
        await using IngredientsWorkspace workspace = Assert.IsType<IngredientsWorkspace>(factory());
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('c');
        workspace.SetField("Name", "Real Gin");
        workspace.SetField("Category", "spirit");
        workspace.SetField("Unit", "oz");
        workspace.SetField("Description", "SQLite and Cedar");
        workspace.SetField("Complete tags (optional)", "audience=sommelier,region=west");
        _ = workspace.Handle(IngredientsWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Page<Ingredient> page = await module.ListAsync(session, new ListIngredientsRequest());
        Ingredient created = Assert.Single(page.Items);
        Assert.Equal("Real Gin", created.Name);
        Assert.Equal(["audience=sommelier", "region=west"], created.Tags.Strings());
        Assert.Contains("Real Gin", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealSqliteCedarInventoryAdjustsAmountCostAndTagsAtomically()
    {
        await using ProductionFixture fixture = await ProductionFixture.CreateAsync();
        MixologySession session = fixture.Session(Actor.Manager);
        IngredientsModule ingredients = fixture.Get<IngredientsModule>();
        InventoryModule inventory = fixture.Get<InventoryModule>();
        Ingredient gin = await ingredients.CreateAsync(
            session,
            new CreateIngredientRequest("Inventory Gin", IngredientCategory.Spirit, Unit.Ounce));
        _ = await inventory.SetAsync(
            session,
            new SetInventoryRequest(gin.Id, Amount.Create(10, Unit.Ounce), new Price(1m, Currency.Usd)));
        Func<ITuiWorkspace> factory = InventoryWorkspace.CreateFactory(
            inventory,
            ingredients,
            fixture.Get<InventoryActionProjector>(),
            fixture.Get<TaggedMutationCoordinator>(),
            session,
            Actor.Manager);
        await using InventoryWorkspace workspace = Assert.IsType<InventoryWorkspace>(factory());
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('a');
        workspace.SetField("Delta", "-2.5");
        workspace.SetField("Unit", "oz");
        workspace.SetField("Cost per unit", "EUR 2.50");
        workspace.SetField("Reason", "used");
        workspace.SetField("Complete tags (optional)", "status=tracked");
        _ = workspace.Handle(InventoryWorkspace.SubmitKey);
        await workspace.DrainAsync();

        InventoryStock adjusted = await inventory.GetAsync(session, gin.Id);
        Assert.Equal(7.5, adjusted.OnHand.Value, 6);
        Assert.Equal(new Price(2.5m, Currency.Eur), adjusted.UnitCost);
        Assert.Equal(["status=tracked"], adjusted.Tags.Strings());
        Assert.Contains("Inventory Gin", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);
    }

    private static Ingredient Ingredient(
        string name,
        IngredientCategory? category = null) => new(
            IngredientId.New(),
            name,
            category ?? IngredientCategory.Spirit,
            Unit.Ounce,
            string.Empty,
            null,
            TagCollection.Empty);

    private static InventoryWorkspaceRow InventoryRow(
        string name,
        double onHand,
        double reserved,
        IngredientCategory? category = null)
    {
        Ingredient ingredient = Ingredient(name, category);
        InventoryStock stock = new(
            InventoryId.New(),
            ingredient.Id,
            Amount.Create(onHand, Unit.Ounce),
            Amount.Create(reserved, Unit.Ounce),
            new Price(1m, Currency.Usd),
            new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
            TagCollection.Empty);
        return new InventoryWorkspaceRow(stock, ingredient);
    }

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

    private sealed class FakeIngredients(IReadOnlyList<Ingredient> rows) : IIngredientsWorkspaceOperations
    {
        public Func<ListIngredientsRequest, CancellationToken, Task<Page<Ingredient>>>? List { get; init; }
        public Func<IngredientId, CancellationToken, Task<Ingredient>>? Get { get; init; }
        public Func<CreateIngredientRequest, TagCollection?, CancellationToken, Task<Ingredient>>? Create { get; init; }
        public List<ListIngredientsRequest> Requests { get; } = [];
        public int CreateCalls { get; private set; }
        public int RetireCalls { get; private set; }
        public RetireIngredientRequest? LastRetirement { get; private set; }

        public Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return List?.Invoke(request, cancellationToken) ?? Task.FromResult(new Page<Ingredient>(rows, default));
        }

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken) =>
            Get?.Invoke(id, cancellationToken) ?? Task.FromResult(rows.Single(row => row.Id == id));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            Ingredient? selected,
            CancellationToken cancellationToken)
        {
            _ = selected;
            _ = cancellationToken;
            return Task.FromResult(IngredientActions());
        }

        public Task<Ingredient> CreateAsync(
            CreateIngredientRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Create?.Invoke(request, desiredTags, cancellationToken) ?? Task.FromResult(rows[0]);
        }

        public Task<Ingredient> UpdateAsync(
            UpdateIngredientRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken)
        {
            _ = request;
            _ = desiredTags;
            _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Ingredient> RetireAsync(
            RetireIngredientRequest request,
            CancellationToken cancellationToken)
        {
            RetireCalls++;
            LastRetirement = request;
            _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }
    }

    private sealed class FakeInventory(IReadOnlyList<InventoryWorkspaceRow> rows) : IInventoryWorkspaceOperations
    {
        public Func<IngredientId, CancellationToken, Task<InventoryWorkspaceRow>>? Get { get; init; }
        public Func<AdjustInventoryRequest, TagCollection?, CancellationToken, Task<InventoryStock>>? Adjust { get; init; }
        public int AdjustCalls { get; private set; }
        public AdjustInventoryRequest? LastAdjustment { get; private set; }
        public TagCollection? LastTags { get; private set; }
        public int SetCalls { get; private set; }
        public SetInventoryRequest? LastSet { get; private set; }
        public TagCollection? LastSetTags { get; private set; }

        public Task<Page<InventoryWorkspaceRow>> ListAsync(
            ListInventoryRequest request,
            CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(new Page<InventoryWorkspaceRow>(rows, default));
        }

        public Task<InventoryWorkspaceRow> GetAsync(IngredientId ingredientId, CancellationToken cancellationToken) =>
            Get?.Invoke(ingredientId, cancellationToken) ??
            Task.FromResult(rows.Single(row => row.Stock.IngredientId == ingredientId));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            InventoryStock? selected,
            CancellationToken cancellationToken)
        {
            _ = selected;
            _ = cancellationToken;
            return Task.FromResult(InventoryActions());
        }

        public Task<InventoryStock> AdjustAsync(
            AdjustInventoryRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken)
        {
            AdjustCalls++;
            LastAdjustment = request;
            LastTags = desiredTags;
            return Adjust?.Invoke(request, desiredTags, cancellationToken) ?? Task.FromResult(rows[0].Stock);
        }

        public Task<InventoryStock> SetAsync(
            SetInventoryRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken)
        {
            SetCalls++;
            LastSet = request;
            LastSetTags = desiredTags;
            _ = cancellationToken;
            return Task.FromResult(rows[0].Stock);
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
            string root = Path.Combine(Path.GetTempPath(), "mixology-tui-workspaces", Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
