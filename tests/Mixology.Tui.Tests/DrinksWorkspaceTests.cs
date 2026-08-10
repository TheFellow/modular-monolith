using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces;
using Mixology.Tui.Workspaces.Drinks;
using Xunit;

namespace Mixology.Tui.Tests;

public sealed class DrinksWorkspaceTests
{
    [Fact]
    public void StructuredRecipeBuildsRequiredOptionalSubstitutesStepsAndGarnish()
    {
        IngredientId gin = IngredientId.New();
        IngredientId vodka = IngredientId.New();
        IngredientId lime = IngredientId.New();
        DrinkRecipeEditor editor = new();
        editor.SetIngredient(0, gin, "2", "oz", optional: false, [vodka]);
        editor.AddIngredient();
        editor.SetIngredient(1, lime, "0.5", "oz", optional: true);
        editor.SetStep(0, "Stir with ice");
        editor.AddStep();
        editor.SetStep(1, "Strain");
        editor.SetGarnish("lime wheel");

        Recipe recipe = editor.Build();

        Assert.Equal(2, recipe.Ingredients.Count);
        Assert.False(recipe.Ingredients[0].Optional);
        Assert.Equal([vodka], recipe.Ingredients[0].Substitutes);
        Assert.True(recipe.Ingredients[1].Optional);
        Assert.Equal(["Stir with ice", "Strain"], recipe.Steps);
        Assert.Equal("lime wheel", recipe.Garnish);
    }

    [Fact]
    public async Task DeferredCatalogRejectsStaleCompletionAndDisposalCancelsLoader()
    {
        Ingredient old = Ingredient("Old");
        Ingredient current = Ingredient("Current");
        TaskCompletionSource<IReadOnlyList<Ingredient>> first = Source<IReadOnlyList<Ingredient>>();
        TaskCompletionSource<IReadOnlyList<Ingredient>> second = Source<IReadOnlyList<Ingredient>>();
        DrinkRecipeEditor editor = new();

        Task firstLoad = editor.LoadCatalogAsync(_ => first.Task);
        Task secondLoad = editor.LoadCatalogAsync(_ => second.Task);
        second.SetResult([current]);
        await secondLoad;
        first.SetResult([old]);
        await firstLoad;

        Assert.Equal(current.Id, Assert.Single(editor.Catalog).Id);

        TaskCompletionSource started = Source();
        bool cancelled = false;
        Task pending = editor.LoadCatalogAsync(async token =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return [];
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                throw;
            }
        });
        await started.Task;
        await editor.DisposeAsync();
        await pending;
        Assert.True(cancelled);
    }

    [Fact]
    public async Task CatalogSearchKeepsIdentitySeparateAndPickerTogglesOptionalAndSubstitutes()
    {
        Ingredient primary = Ingredient("Gin, London Dry");
        Ingredient substitute = Ingredient("Vodka");
        await using DrinkRecipeEditor editor = new();
        await editor.LoadCatalogAsync(_ => Task.FromResult<IReadOnlyList<Ingredient>>([primary, substitute]));

        IngredientOption found = Assert.Single(editor.SearchCatalog("London"));
        editor.SelectIngredient(0, found.Id);
        editor.ToggleOptional(0);
        editor.ToggleSubstitute(0, substitute.Id);
        editor.SetIngredient(
            0,
            editor.Ingredients[0].IngredientId,
            "1.5",
            "oz",
            editor.Ingredients[0].Optional,
            editor.Ingredients[0].Substitutes);
        editor.SetStep(0, "Stir");

        RecipeIngredient value = Assert.Single(editor.Build().Ingredients);
        Assert.Equal(primary.Id, value.IngredientId);
        Assert.True(value.Optional);
        Assert.Equal([substitute.Id], value.Substitutes);
    }

    [Fact]
    public async Task DetailRejectsStaleSelectionAndRenderingIsBoundedAtEightyByTwentyOne()
    {
        Drink first = DrinkValue("First");
        Drink second = DrinkValue("Second", DrinkCategory.Mocktail);
        TaskCompletionSource<Drink> firstDetail = Source<Drink>();
        TaskCompletionSource<Drink> secondDetail = Source<Drink>();
        FakeOperations operations = new([first, second])
        {
            Get = (id, _) => id == first.Id ? firstDetail.Task : secondDetail.Task,
        };
        await using DrinksWorkspace workspace = new(operations);

        await workspace.ActivateAsync();
        _ = workspace.Handle('j');
        secondDetail.SetResult(second);
        await UntilAsync(() => workspace.Render(new Viewport(80, 21)).Contains("Category: mocktail", StringComparison.Ordinal));
        firstDetail.SetResult(first);
        await workspace.DrainAsync();

        string rendered = workspace.Render(new Viewport(80, 21));
        Assert.Equal(rendered, workspace.Render(new Viewport(80, 21)));
        Assert.True(rendered.Split('\n').Length <= 21);
        Assert.All(rendered.Split('\n'), static line => Assert.True(line.Length <= 80));
        Assert.Contains("Second", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Category: cocktail", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListGenerationRejectsLateRefreshResponse()
    {
        Drink old = DrinkValue("Old");
        Drink current = DrinkValue("Current");
        TaskCompletionSource<Page<Drink>> first = Source<Page<Drink>>();
        TaskCompletionSource<Page<Drink>> second = Source<Page<Drink>>();
        int calls = 0;
        FakeOperations operations = new([])
        {
            List = (_, _) => Interlocked.Increment(ref calls) == 1 ? first.Task : second.Task,
        };
        await using DrinksWorkspace workspace = new(operations);

        Task firstLoad = workspace.ActivateAsync();
        Task currentLoad = workspace.RefreshAsync();
        second.SetResult(new Page<Drink>([current], default));
        await currentLoad;
        first.SetResult(new Page<Drink>([old], default));
        await firstLoad;
        await workspace.DrainAsync();

        Assert.Equal(current.Id, Assert.Single(workspace.Rows).Id);
    }

    [Fact]
    public async Task WorkspaceDisposalCancelsAndDrainsOutstandingList()
    {
        TaskCompletionSource started = Source();
        bool cancelled = false;
        FakeOperations operations = new([])
        {
            List = async (_, token) =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return new Page<Drink>([], default);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    throw;
                }
            },
        };
        DrinksWorkspace workspace = new(operations);
        _ = workspace.ActivateAsync();
        await started.Task;

        await workspace.DisposeAsync();

        Assert.True(cancelled);
    }

    [Fact]
    public async Task CreateOwnsInputGatesDuplicateSubmissionAndIgnoresEscapeWhileSubmitting()
    {
        Drink existing = DrinkValue("Existing");
        TaskCompletionSource<Drink> completion = Source<Drink>();
        FakeOperations operations = new([existing]) { Create = (_, _, _) => completion.Task };
        await using DrinksWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        Assert.True(workspace.Handle('c'));
        Assert.Equal(InputOwnership.Edit, workspace.InputOwnership);
        workspace.SetField("Name", "New Drink");
        workspace.SetField("Category", "cocktail");
        workspace.SetField("Glass", "coupe");
        workspace.RecipeEditor!.SetIngredient(0, IngredientId.New(), "1", "oz", false);
        workspace.RecipeEditor.SetStep(0, "Shake");
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);
        _ = workspace.Handle('\u001b');

        Assert.Equal(1, operations.CreateCalls);
        Assert.Equal(DrinksWorkspaceMode.Submitting, workspace.Mode);
        completion.SetResult(DrinkValue("New Drink"));
        await workspace.DrainAsync();
        Assert.Equal(DrinksWorkspaceMode.Browse, workspace.Mode);
    }

    [Fact]
    public async Task NestedRecipeModeAuthorsCompleteRecipeThroughHeadlessKeys()
    {
        Ingredient gin = Ingredient("Gin");
        Ingredient vodka = Ingredient("Vodka");
        Drink existing = DrinkValue("Existing");
        FakeOperations operations = new([existing]) { Catalog = [gin, vodka] };
        await using DrinksWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();
        _ = workspace.Handle('c');
        workspace.SetField("Name", "Keyboard Martini");
        workspace.SetField("Category", "martini");
        workspace.SetField("Glass", "martini");

        _ = workspace.Handle(DrinksWorkspace.RecipeKey);
        Assert.True(workspace.RecipeInputActive);
        _ = workspace.Handle('\u001b');
        Assert.False(workspace.RecipeInputActive);
        Assert.Equal(DrinksWorkspaceMode.Create, workspace.Mode);

        _ = workspace.Handle(DrinksWorkspace.RecipeKey);
        Type(workspace, "Gin");
        _ = workspace.Handle('\t');
        Type(workspace, "2");
        _ = workspace.Handle('\t');
        _ = workspace.Handle('\t');
        _ = workspace.Handle('\b');
        _ = workspace.Handle('\b');
        Type(workspace, "yes");
        _ = workspace.Handle('\t');
        Type(workspace, "Vodka");
        _ = workspace.Handle('\t');
        Type(workspace, "Stir with ice");
        _ = workspace.Handle('\t');
        Type(workspace, "olive");
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);

        Assert.False(workspace.RecipeInputActive);
        Assert.Equal(DrinksWorkspaceMode.Create, workspace.Mode);
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Recipe recipe = Assert.IsType<CreateDrinkRequest>(operations.LastCreate).Recipe;
        RecipeIngredient ingredient = Assert.Single(recipe.Ingredients);
        Assert.Equal(gin.Id, ingredient.IngredientId);
        Assert.Equal([vodka.Id], ingredient.Substitutes);
        Assert.True(ingredient.Optional);
        Assert.Equal(2, ingredient.Amount.Value);
        Assert.Equal(["Stir with ice"], recipe.Steps);
        Assert.Equal("olive", recipe.Garnish);
    }

    [Fact]
    public async Task FilterPagingActionProjectionAndSafeErrorsRemainSurfaceOwned()
    {
        Drink first = DrinkValue("First");
        Cursor cursor = new(first.Id.Value);
        FakeOperations operations = new([first])
        {
            List = (request, _) => Task.FromResult(new Page<Drink>([first], request.Cursor.IsEmpty ? cursor : default)),
            Actions =
            [
                new(DrinkActionProjector.ListAction, true, true),
                new(DrinkActionProjector.CreateAction, true, false, "license required"),
                new(DrinkActionProjector.EditAction, false, false),
                new(DrinkActionProjector.DeleteAction, true, false, "in use"),
            ],
        };
        await using DrinksWorkspace workspace = new(operations);
        await workspace.ActivateAsync();
        await workspace.DrainAsync();
        string browse = workspace.Render(new Viewport(80, 21));
        Assert.Contains("[c] create disabled: license required", browse, StringComparison.Ordinal);
        Assert.DoesNotContain("[e] edit", browse, StringComparison.Ordinal);
        Assert.Contains("[d] delete disabled: in use", browse, StringComparison.Ordinal);

        _ = workspace.Handle(']');
        await workspace.DrainAsync();
        Assert.Equal(cursor, operations.Requests[^1].Cursor);
        _ = workspace.Handle('[');
        await workspace.DrainAsync();
        Assert.True(operations.Requests[^1].Cursor.IsEmpty);
        _ = workspace.Handle('h');
        Assert.Contains("recipe.garnish", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);

        FakeOperations broken = new([first])
        {
            List = (_, _) => Task.FromException<Page<Drink>>(new IOException("secret path")),
        };
        await using DrinksWorkspace safe = new(broken);
        await safe.ActivateAsync();
        Assert.Equal("internal error", safe.Status?.Message);
    }

    [Fact]
    public async Task RealSqliteCedarCrudTagsAndFilterFlowThroughPublicModules()
    {
        await using ProductionFixture fixture = await ProductionFixture.CreateAsync();
        MixologySession session = fixture.Session(Actor.Manager);
        IngredientsModule ingredients = fixture.Get<IngredientsModule>();
        DrinksModule drinks = fixture.Get<DrinksModule>();
        Ingredient gin = await ingredients.CreateAsync(
            session,
            new CreateIngredientRequest("TUI Gin", IngredientCategory.Spirit, Unit.Ounce));
        Ingredient vodka = await ingredients.CreateAsync(
            session,
            new CreateIngredientRequest("TUI Vodka", IngredientCategory.Spirit, Unit.Ounce));
        Func<ITuiWorkspace> factory = DrinksWorkspace.CreateFactory(
            drinks,
            ingredients,
            fixture.Get<DrinkActionProjector>(),
            fixture.Get<TaggedMutationCoordinator>(),
            session,
            Actor.Manager);
        await using DrinksWorkspace workspace = Assert.IsType<DrinksWorkspace>(factory());
        await workspace.ActivateAsync();
        await workspace.DrainAsync();

        _ = workspace.Handle('c');
        workspace.SetField("Name", "TUI Martini");
        workspace.SetField("Category", "martini");
        workspace.SetField("Glass", "martini");
        workspace.SetField("Description", "created from workspace");
        workspace.SetField("Complete tags (optional)", "featured,region=west");
        workspace.RecipeEditor!.SetIngredient(0, gin.Id, "2", "oz", false, [vodka.Id]);
        workspace.RecipeEditor.SetStep(0, "Stir with ice");
        workspace.RecipeEditor.SetGarnish("olive");
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);
        await workspace.DrainAsync();

        Drink created = Assert.Single((await drinks.ListAsync(
            session,
            new ListDrinksRequest(Filter: "tags contains \"featured\""))).Items);
        Assert.Equal([vodka.Id], Assert.Single(created.Recipe.Ingredients).Substitutes);
        Assert.Equal(["featured", "region=west"], created.Tags.Strings());

        _ = workspace.Handle('e');
        workspace.SetField("Name", "TUI Martini Updated");
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);
        await workspace.DrainAsync();
        Assert.Equal("TUI Martini Updated", (await drinks.GetAsync(session, created.Id)).Name);
        Assert.Equal(["featured", "region=west"], (await drinks.GetAsync(session, created.Id)).Tags.Strings());

        _ = workspace.Handle('f');
        workspace.SetField("Expression", "recipe.garnish == \"olive\"");
        _ = workspace.Handle(DrinksWorkspace.SubmitKey);
        await workspace.DrainAsync();
        Assert.Contains("TUI Martini Updated", workspace.Render(new Viewport(80, 21)), StringComparison.Ordinal);

        _ = workspace.Handle('d');
        _ = workspace.Handle('y');
        await workspace.DrainAsync();
        await Assert.ThrowsAsync<NotFoundError>(() => drinks.GetAsync(session, created.Id));
    }

    private static Drink DrinkValue(string name, DrinkCategory? category = null) => new(
        DrinkId.New(),
        name,
        category ?? DrinkCategory.Cocktail,
        GlassType.Coupe,
        new Recipe(
            [new RecipeIngredient(IngredientId.New(), Amount.Create(1, Unit.Ounce))],
            ["Shake"]),
        string.Empty,
        DrinkStatus.Active,
        null,
        TagCollection.Empty);

    private static Ingredient Ingredient(string name) => new(
        IngredientId.New(),
        name,
        IngredientCategory.Spirit,
        Unit.Ounce,
        string.Empty,
        null,
        TagCollection.Empty);

    private static IReadOnlyList<ActionState> FullActions() =>
    [
        new(DrinkActionProjector.ListAction, true, true),
        new(DrinkActionProjector.CreateAction, true, true),
        new(DrinkActionProjector.EditAction, true, true),
        new(DrinkActionProjector.DeleteAction, true, true),
        new(DrinkActionProjector.TagsAction, true, true),
    ];

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource Source() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task UntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition()) { await Task.Delay(10, timeout.Token); }
    }

    private static void Type(DrinksWorkspace workspace, string value)
    {
        foreach (char character in value) { _ = workspace.Handle(character); }
    }

    private sealed class FakeOperations(IReadOnlyList<Drink> rows) : IDrinksWorkspaceOperations
    {
        public Func<ListDrinksRequest, CancellationToken, Task<Page<Drink>>>? List { get; init; }
        public Func<DrinkId, CancellationToken, Task<Drink>>? Get { get; init; }
        public Func<CreateDrinkRequest, TagCollection?, CancellationToken, Task<Drink>>? Create { get; init; }
        public IReadOnlyList<Ingredient> Catalog { get; init; } = [];
        public IReadOnlyList<ActionState> Actions { get; init; } = FullActions();
        public List<ListDrinksRequest> Requests { get; } = [];
        public int CreateCalls { get; private set; }
        public CreateDrinkRequest? LastCreate { get; private set; }

        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return List?.Invoke(request, cancellationToken) ?? Task.FromResult(new Page<Drink>(rows, default));
        }

        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
            Get?.Invoke(id, cancellationToken) ?? Task.FromResult(rows.Single(value => value.Id == id));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(Drink? selected, CancellationToken cancellationToken)
        {
            _ = selected;
            _ = cancellationToken;
            return Task.FromResult(Actions);
        }

        public Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(Catalog);
        }

        public Task<Drink> CreateAsync(
            CreateDrinkRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            LastCreate = request;
            return Create?.Invoke(request, tags, cancellationToken) ?? Task.FromResult(rows[0]);
        }

        public Task<Drink> UpdateAsync(
            UpdateDrinkRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken)
        {
            _ = request;
            _ = tags;
            _ = cancellationToken;
            return Task.FromResult(rows[0]);
        }

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken)
        {
            _ = id;
            _ = cancellationToken;
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
            string root = Path.Combine(Path.GetTempPath(), "mixology-drinks-tui", Guid.NewGuid().ToString("N"));
            TuiOptions options = TuiOptions.Create(
                Path.Combine(root, "mixology.db"),
                "manager",
                "error",
                "text",
                Path.Combine(root, "mixology.log"),
                metrics: false);
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
