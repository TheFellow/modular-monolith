using Microsoft.Extensions.DependencyInjection;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Desktop.Workspaces;
using Mixology.Desktop.Workspaces.Drinks;
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
using Xunit;

namespace Mixology.Desktop.Tests;

public sealed class DrinksWorkspaceViewModelTests
{
    [Fact]
    public async Task LatestListWinsAndSelectionIsRetainedByStableId()
    {
        Drink first = DrinkNamed("First");
        Drink second = DrinkNamed("Second");
        TaskCompletionSource<Page<Drink>> stale = Source<Page<Drink>>();
        TaskCompletionSource<Page<Drink>> current = Source<Page<Drink>>();
        Queue<TaskCompletionSource<Page<Drink>>> loads = new([stale, current]);
        FakeOperations operations = new()
        {
            List = (_, _) => loads.Dequeue().Task,
        };
        await using DrinksWorkspaceViewModel viewModel = new(operations);

        Task oldRefresh = viewModel.RefreshAsync();
        Task newRefresh = viewModel.RefreshAsync();
        current.SetResult(new Page<Drink>([first, second], default));
        await newRefresh;
        viewModel.SelectedItem = viewModel.Items.Single(item => item.Id == second.Id);
        await viewModel.LoadSelectedAsync(viewModel.SelectedItem);
        stale.SetResult(new Page<Drink>([DrinkNamed("Stale")], default));
        await oldRefresh;

        Assert.Equal(["First", "Second"], viewModel.Items.Select(static item => item.Name));
        Assert.Equal(second.Id, viewModel.SelectedItem?.Id);
    }

    [Fact]
    public async Task LatestDetailAndCatalogResponsesCannotOverwriteCurrentState()
    {
        Drink first = DrinkNamed("First");
        Drink second = DrinkNamed("Second");
        Ingredient oldIngredient = IngredientNamed("Old");
        Ingredient newIngredient = IngredientNamed("New");
        FakeOperations operations = new()
        {
            Page = new Page<Drink>([first, second], default),
        };
        await using DrinksWorkspaceViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await viewModel.LoadSelectedAsync(viewModel.SelectedItem);

        TaskCompletionSource<Drink> staleDetail = Source<Drink>();
        TaskCompletionSource<Drink> currentDetail = Source<Drink>();
        operations.Get = (id, _) => id == first.Id ? staleDetail.Task : currentDetail.Task;
        Task oldSelection = viewModel.LoadSelectedAsync(viewModel.Items[0]);
        Task newSelection = viewModel.LoadSelectedAsync(viewModel.Items[1]);
        currentDetail.SetResult(second);
        await newSelection;
        staleDetail.SetResult(first);
        await oldSelection;
        Assert.Equal(second.Id, viewModel.Detail?.Id);

        TaskCompletionSource<IReadOnlyList<Ingredient>> staleCatalog = Source<IReadOnlyList<Ingredient>>();
        TaskCompletionSource<IReadOnlyList<Ingredient>> currentCatalog = Source<IReadOnlyList<Ingredient>>();
        Queue<TaskCompletionSource<IReadOnlyList<Ingredient>>> catalogs = new([staleCatalog, currentCatalog]);
        operations.Catalog = _ => catalogs.Dequeue().Task;
        Task oldCreate = viewModel.StartCreateAsync();
        Task newCreate = viewModel.StartCreateAsync();
        currentCatalog.SetResult([newIngredient]);
        await newCreate;
        staleCatalog.SetResult([oldIngredient]);
        await oldCreate;

        RecipeIngredientViewModel row = Assert.Single(viewModel.Recipe!.Ingredients);
        Assert.Equal("New", Assert.Single(row.IngredientMatches).Name);
    }

    [Fact]
    public async Task RecipeEditorRoundTripsStructuredOptionalSubstitutesStepsAndGarnish()
    {
        Ingredient baseIngredient = IngredientNamed("Base");
        Ingredient substitute = IngredientNamed("Alternative");
        FakeOperations operations = new()
        {
            CatalogItems = [baseIngredient, substitute],
        };
        await using DrinksWorkspaceViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await viewModel.StartCreateAsync();
        RecipeIngredientViewModel row = Assert.Single(viewModel.Recipe!.Ingredients);
        row.IngredientSearch = "Bas";
        row.SelectedIngredient = Assert.Single(row.IngredientMatches);
        row.Amount = "1.5";
        row.SelectedUnit = Unit.Ounce.Value;
        row.IsOptional = true;
        Assert.Single(row.SubstituteOptions).IsSelected = true;
        viewModel.Recipe.Steps[0].Text = "Shake";
        viewModel.Recipe.AddStepCommand.Execute(null);
        viewModel.Recipe.Steps[1].Text = "Strain";
        viewModel.Recipe.Garnish = "Lime twist";

        Recipe recipe = viewModel.Recipe.Build();

        RecipeIngredient built = Assert.Single(recipe.Ingredients);
        Assert.Equal(baseIngredient.Id, built.IngredientId);
        Assert.True(built.Optional);
        Assert.Equal(1.5, built.Amount.Value);
        Assert.Equal(substitute.Id, Assert.Single(built.Substitutes));
        Assert.Equal(["Shake", "Strain"], recipe.Steps);
        Assert.Equal("Lime twist", recipe.Garnish);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task DuplicateSubmissionIsIgnoredAndAcceptedMutationIsDrained()
    {
        Ingredient ingredient = IngredientNamed("Gin");
        TaskCompletionSource<Drink> completion = Source<Drink>();
        FakeOperations operations = new()
        {
            CatalogItems = [ingredient],
            Create = (_, _, _) => completion.Task,
        };
        DrinksWorkspaceViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await viewModel.StartCreateAsync();
        FillValidForm(viewModel, ingredient);

        Task first = viewModel.SaveAsync();
        Task duplicate = viewModel.SaveAsync();
        await duplicate;
        Assert.Equal(1, operations.CreateCount);
        completion.SetResult(DrinkNamed("Daiquiri"));
        await first;
        await viewModel.DisposeAsync();

        Assert.Equal(1, operations.CreateCount);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task FiltersAndCursorHistoryResetPredictably()
    {
        Drink first = DrinkNamed("First");
        Drink second = DrinkNamed("Second");
        List<ListDrinksRequest> requests = [];
        FakeOperations operations = new()
        {
            List = (request, _) =>
            {
                requests.Add(request);
                return Task.FromResult(request.Cursor.IsEmpty
                    ? new Page<Drink>([first], second.Id.Value)
                    : new Page<Drink>([second], default));
            },
        };
        await using DrinksWorkspaceViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        Assert.True(viewModel.HasNextPage);

        await viewModel.NextPageAsync();
        Assert.True(viewModel.HasPreviousPage);
        Assert.Equal(second.Id.Value, requests[^1].Cursor.Value);
        viewModel.ExpressionFilter = "name.contains(\"First\")";
        viewModel.ShowFilterHelp = true;
        await viewModel.ApplyFilterAsync();

        Assert.True(requests[^1].Cursor.IsEmpty);
        Assert.Equal("name.contains(\"First\")", requests[^1].Filter);
        Assert.False(viewModel.ShowFilterHelp);
        Assert.False(viewModel.HasPreviousPage);
    }

    [Fact]
    public async Task DisposalCancelsAndDrainsAcceptedMutation()
    {
        Ingredient ingredient = IngredientNamed("Gin");
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        FakeOperations operations = new()
        {
            CatalogItems = [ingredient],
            Create = async (_, _, token) =>
            {
                observed = token;
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DrinkNamed("never");
            },
        };
        DrinksWorkspaceViewModel viewModel = new(operations);
        await viewModel.ActivateAsync();
        await viewModel.StartCreateAsync();
        FillValidForm(viewModel, ingredient);
        Task save = viewModel.SaveAsync();
        await started.Task;

        await viewModel.DisposeAsync();

        Assert.True(observed.IsCancellationRequested);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
    }

    [Fact]
    public async Task TypedErrorsKeepIdentityAndUnknownErrorsAreSafeInternalErrors()
    {
        InvalidError invalid = AppError.Invalid("bad drink filter");
        FakeOperations typed = new() { List = (_, _) => Task.FromException<Page<Drink>>(invalid) };
        await using DrinksWorkspaceViewModel typedViewModel = new(typed);
        await typedViewModel.RefreshAsync();
        Assert.Same(invalid, typedViewModel.Error);
        Assert.Equal("bad drink filter", typedViewModel.StatusMessage);

        InvalidOperationException cause = new("database password");
        FakeOperations unknown = new() { List = (_, _) => Task.FromException<Page<Drink>>(cause) };
        await using DrinksWorkspaceViewModel unknownViewModel = new(unknown);
        await unknownViewModel.RefreshAsync();
        InternalError error = Assert.IsType<InternalError>(unknownViewModel.Error);
        Assert.Same(cause, error.InnerException);
        Assert.Equal("internal error", unknownViewModel.StatusMessage);
    }

    [Fact]
    public async Task RealSqliteCedarAndTaggedMutationPersistFullRecipeAndTagReplacement()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-desktop-drinks", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        try
        {
            await using DesktopHost host = await DesktopHost.OpenAsync(
                DesktopOptions.Create(database, "owner"),
                TestContext.Current.CancellationToken);
            IServiceProvider services = host.Services;
            MixologySession session = services.GetRequiredService<MixologySessionFactory>().Create(Actor.Owner);
            IngredientsModule ingredients = services.GetRequiredService<IngredientsModule>();
            Ingredient gin = await ingredients.CreateAsync(
                session,
                new CreateIngredientRequest("Gin", IngredientCategory.Spirit, Unit.Ounce),
                TestContext.Current.CancellationToken);
            Ingredient vodka = await ingredients.CreateAsync(
                session,
                new CreateIngredientRequest("Vodka", IngredientCategory.Spirit, Unit.Ounce),
                TestContext.Current.CancellationToken);
            Func<IDesktopWorkspace> factory = DrinksWorkspaceViewModel.CreateFactory(
                services.GetRequiredService<DrinksModule>(),
                ingredients,
                services.GetRequiredService<DrinkActionProjector>(),
                services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                Actor.Owner);
            await using DrinksWorkspaceViewModel viewModel = Assert.IsType<DrinksWorkspaceViewModel>(factory());
            await viewModel.ActivateAsync(TestContext.Current.CancellationToken);
            await viewModel.StartCreateAsync(TestContext.Current.CancellationToken);
            viewModel.Name = "Martinez";
            viewModel.Category = DrinkCategory.Cocktail.Value;
            viewModel.Glass = GlassType.Coupe.Value;
            viewModel.Description = "Spirit-forward";
            viewModel.Tags = "featured,region=west";
            RecipeIngredientViewModel row = Assert.Single(viewModel.Recipe!.Ingredients);
            row.IngredientSearch = "Gin";
            row.SelectedIngredient = row.IngredientMatches.Single(option => option.Id == gin.Id);
            row.Amount = "2";
            row.SubstituteOptions.Single(option => option.Option.Id == vodka.Id).IsSelected = true;
            viewModel.Recipe.Steps[0].Text = "Stir with ice";
            viewModel.Recipe.Garnish = "Orange twist";

            await viewModel.SaveAsync(TestContext.Current.CancellationToken);

            Drink created = Assert.Single((await services.GetRequiredService<DrinksModule>().ListAsync(
                session,
                new ListDrinksRequest(Name: "Martinez"),
                TestContext.Current.CancellationToken)).Items);
            Assert.Equal("featured,region=west", created.Tags.Format());
            RecipeIngredient persisted = Assert.Single(created.Recipe.Ingredients);
            Assert.Equal(gin.Id, persisted.IngredientId);
            Assert.Equal(vodka.Id, Assert.Single(persisted.Substitutes));
            Assert.Equal("Orange twist", created.Recipe.Garnish);

            await viewModel.LoadSelectedAsync(viewModel.SelectedItem, TestContext.Current.CancellationToken);
            await viewModel.StartEditAsync(TestContext.Current.CancellationToken);
            viewModel.Tags = string.Empty;
            await viewModel.SaveAsync(TestContext.Current.CancellationToken);
            Drink cleared = await services.GetRequiredService<DrinksModule>().GetAsync(
                session,
                created.Id,
                TestContext.Current.CancellationToken);
            Assert.Empty(cleared.Tags);

            await viewModel.LoadSelectedAsync(viewModel.SelectedItem, TestContext.Current.CancellationToken);
            viewModel.BeginDelete();
            await viewModel.ConfirmDeleteAsync(TestContext.Current.CancellationToken);
            Exception deleted = await Assert.ThrowsAsync<NotFoundError>(() =>
                services.GetRequiredService<DrinksModule>().GetAsync(
                    session,
                    created.Id,
                    TestContext.Current.CancellationToken));
            Assert.True(AppError.IsNotFound(deleted));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void FillValidForm(DrinksWorkspaceViewModel viewModel, Ingredient ingredient)
    {
        viewModel.Name = "Daiquiri";
        viewModel.Category = DrinkCategory.Cocktail.Value;
        viewModel.Glass = GlassType.Coupe.Value;
        RecipeIngredientViewModel row = Assert.Single(viewModel.Recipe!.Ingredients);
        row.SelectedIngredient = row.IngredientMatches.Single(option => option.Id == ingredient.Id);
        row.Amount = "2";
        viewModel.Recipe.Steps[0].Text = "Shake";
    }

    private static Drink DrinkNamed(string name) => new(
        DrinkId.New(),
        name,
        DrinkCategory.Cocktail,
        GlassType.Coupe,
        new Recipe(
            [new RecipeIngredient(IngredientId.New(), Amount.Create(1, Unit.Ounce))],
            ["Stir"]),
        string.Empty,
        DrinkStatus.Active,
        null,
        TagCollection.Empty);

    private static Ingredient IngredientNamed(string name) => new(
        IngredientId.New(),
        name,
        IngredientCategory.Spirit,
        Unit.Ounce,
        string.Empty,
        null,
        TagCollection.Empty);

    private static TaskCompletionSource<T> Source<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FakeOperations : IDrinksWorkspaceOperations
    {
        public Page<Drink> Page { get; init; } = new([], default);

        public IReadOnlyList<Ingredient> CatalogItems { get; init; } = [];

        public Func<ListDrinksRequest, CancellationToken, Task<Page<Drink>>>? List { get; init; }

        public Func<DrinkId, CancellationToken, Task<Drink>>? Get { get; set; }

        public Func<CancellationToken, Task<IReadOnlyList<Ingredient>>>? Catalog { get; set; }

        public Func<CreateDrinkRequest, TagCollection?, CancellationToken, Task<Drink>>? Create { get; init; }

        public int CreateCount { get; private set; }

        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken) =>
            List?.Invoke(request, cancellationToken) ?? Task.FromResult(Page);

        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
            Get?.Invoke(id, cancellationToken)
                ?? Task.FromResult(Page.Items.Single(value => value.Id == id));

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            Drink? selected,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<ActionState> actions =
            [
                new(DrinkActionProjector.ListAction, true, true),
                new(DrinkActionProjector.CreateAction, true, true),
            ];
            if (selected is not null)
            {
                actions.Add(new ActionState(DrinkActionProjector.EditAction, true, true));
                actions.Add(new ActionState(DrinkActionProjector.DeleteAction, true, true));
                actions.Add(new ActionState(DrinkActionProjector.TagsAction, true, true));
            }

            return Task.FromResult<IReadOnlyList<ActionState>>(actions);
        }

        public Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(CancellationToken cancellationToken) =>
            Catalog?.Invoke(cancellationToken) ?? Task.FromResult(CatalogItems);

        public Task<Drink> CreateAsync(
            CreateDrinkRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            return Create?.Invoke(request, desiredTags, cancellationToken)
                ?? Task.FromResult(new Drink(
                    DrinkId.New(),
                    request.Name,
                    request.Category,
                    request.Glass,
                    request.Recipe,
                    request.Description,
                    DrinkStatus.Active,
                    null,
                    desiredTags ?? TagCollection.Empty));
        }

        public Task<Drink> UpdateAsync(
            UpdateDrinkRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => Task.FromResult(new Drink(
                request.Id,
                request.Name,
                request.Category,
                request.Glass,
                request.Recipe,
                request.Description,
                DrinkStatus.Active,
                null,
                desiredTags ?? TagCollection.Empty));

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken) =>
            Task.FromResult(Page.Items.Single(value => value.Id == id));
    }
}
