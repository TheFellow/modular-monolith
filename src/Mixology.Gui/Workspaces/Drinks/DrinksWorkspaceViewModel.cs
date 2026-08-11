using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Gui.Workspaces.Drinks;

public sealed record DrinkRecipeIngredientDetailViewModel(
    string Name,
    string Amount,
    string OptionalLabel);

public enum DrinksDesktopMode
{
    Browse,
    Detail,
    Create,
    Edit,
    DeleteConfirmation,
}

public sealed class DrinksWorkspaceViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly IDrinksWorkspaceOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<ListLoadOutcome> listRequests = new();
    private readonly LatestRequest<DetailLoadOutcome> detailRequests = new();
    private readonly LatestRequest<CatalogLoadOutcome> catalogRequests = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<Cursor> history = [];
    private readonly object mutationSync = new();
    private readonly HashSet<Task> mutations = [];
    private DrinksDesktopMode mode;
    private DrinkListItemViewModel? selectedItem;
    private Drink? detail;
    private DrinkRecipeEditorViewModel? recipe;
    private bool isLoading;
    private bool isCatalogLoading;
    private bool isSubmitting;
    private bool isDirty;
    private bool showFilters;
    private bool showFilterHelp;
    private bool updatingForm;
    private bool tagsDirty;
    private bool disposed;
    private int submitting;
    private Cursor next;
    private DrinkId pendingSelection;
    private string name = string.Empty;
    private string category = DrinkCategory.Cocktail.Value;
    private string glass = GlassType.Rocks.Value;
    private string description = string.Empty;
    private string tags = string.Empty;
    private bool replaceTags = true;
    private string exactNameFilter = string.Empty;
    private string categoryFilter = string.Empty;
    private string glassFilter = string.Empty;
    private string expressionFilter = string.Empty;
    private string pageSize = PageRequest.DefaultLimit.ToString(CultureInfo.InvariantCulture);
    private string statusMessage = string.Empty;
    private Exception? error;
    private DrinkActionViewModel? createAction;
    private DrinkActionViewModel? editAction;
    private DrinkActionViewModel? deleteAction;
    private DrinkActionViewModel? tagsAction;

    public DrinksWorkspaceViewModel(
        IDrinksWorkspaceOperations operations,
        IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ClearFilterCommand = new AsyncRelayCommand(ClearFilterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        StartCreateCommand = new AsyncRelayCommand(StartCreateAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        StartEditCommand = new AsyncRelayCommand(StartEditAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        BeginDeleteCommand = new RelayCommand(BeginDelete);
        ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        SaveCommand = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CancelCommand = new RelayCommand(Cancel);
        ToggleFiltersCommand = new RelayCommand(() => ShowFilters = !ShowFilters);
        ToggleFilterHelpCommand = new RelayCommand(() => ShowFilterHelp = !ShowFilterHelp);
        RemoveIngredientCommand = new RelayCommand<RecipeIngredientViewModel>(RemoveIngredient);
        RemoveStepCommand = new RelayCommand<RecipeStepViewModel>(RemoveStep);
        MoveStepUpCommand = new RelayCommand<RecipeStepViewModel>(step => Recipe?.MoveStep(step!, -1));
        MoveStepDownCommand = new RelayCommand<RecipeStepViewModel>(step => Recipe?.MoveStep(step!, 1));
    }

    public static Func<IDesktopWorkspace> CreateFactory(
        DrinksModule drinks,
        IngredientsModule ingredients,
        DrinkActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(drinks);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new DrinksWorkspaceViewModel(
            new ModuleDrinksWorkspaceOperations(
                drinks,
                ingredients,
                projector,
                taggedMutations,
                session,
                actor),
            dispatcher);
    }

    public WorkspaceId Id => NavigationProjector.DrinksWorkspace;

    public string Title => "Drinks";

    public bool IsDirty => isDirty;

    public ObservableCollection<DrinkListItemViewModel> Items { get; } = [];

    public ObservableCollection<DrinkRecipeIngredientDetailViewModel> DetailIngredients { get; } = [];

    public ObservableCollection<DrinkActionViewModel> Actions { get; } = [];

    public IReadOnlyList<string> Categories { get; } = DrinkCategory.All.Select(static value => value.Value).ToArray();

    public IReadOnlyList<string> Glasses { get; } = GlassType.All.Select(static value => value.Value).ToArray();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ApplyFilterCommand { get; }

    public IAsyncRelayCommand ClearFilterCommand { get; }

    public IAsyncRelayCommand NextPageCommand { get; }

    public IAsyncRelayCommand PreviousPageCommand { get; }

    public IAsyncRelayCommand StartCreateCommand { get; }

    public IAsyncRelayCommand StartEditCommand { get; }

    public IRelayCommand BeginDeleteCommand { get; }

    public IAsyncRelayCommand ConfirmDeleteCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand ToggleFiltersCommand { get; }

    public IRelayCommand ToggleFilterHelpCommand { get; }

    public IRelayCommand<RecipeIngredientViewModel> RemoveIngredientCommand { get; }

    public IRelayCommand<RecipeStepViewModel> RemoveStepCommand { get; }

    public IRelayCommand<RecipeStepViewModel> MoveStepUpCommand { get; }

    public IRelayCommand<RecipeStepViewModel> MoveStepDownCommand { get; }

    public DrinksDesktopMode Mode
    {
        get => mode;
        private set
        {
            if (!SetProperty(ref mode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsBrowse));
            OnPropertyChanged(nameof(IsDetail));
            OnPropertyChanged(nameof(IsForm));
            OnPropertyChanged(nameof(IsDeleteConfirmation));
            OnPropertyChanged(nameof(FormTitle));
            OnPropertyChanged(nameof(CanEditTags));
            OnPropertyChanged(nameof(CanReplaceTags));
        }
    }

    public bool IsBrowse => Mode == DrinksDesktopMode.Browse;

    public bool IsDetail => Mode == DrinksDesktopMode.Detail;

    public bool IsForm => Mode is DrinksDesktopMode.Create or DrinksDesktopMode.Edit;

    public bool IsDeleteConfirmation => Mode == DrinksDesktopMode.DeleteConfirmation;

    public string FormTitle => Mode == DrinksDesktopMode.Create ? "Create drink" : "Edit drink";

    public bool CanEditTags => Mode == DrinksDesktopMode.Create || TagsAction is { Visible: true };

    public bool CanReplaceTags => Mode == DrinksDesktopMode.Create || TagsAction is { Visible: true, Enabled: true };

    public DrinkListItemViewModel? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (SetProperty(ref selectedItem, value))
            {
                _ = LoadSelectedAsync(value);
            }
        }
    }

    public Drink? Detail
    {
        get => detail;
        private set => SetProperty(ref detail, value);
    }

    public DrinkRecipeEditorViewModel? Recipe
    {
        get => recipe;
        private set => SetProperty(ref recipe, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool IsCatalogLoading
    {
        get => isCatalogLoading;
        private set => SetProperty(ref isCatalogLoading, value);
    }

    public bool IsSubmitting
    {
        get => isSubmitting;
        private set => SetProperty(ref isSubmitting, value);
    }

    public bool ShowFilters
    {
        get => showFilters;
        set => SetProperty(ref showFilters, value);
    }

    public bool ShowFilterHelp
    {
        get => showFilterHelp;
        set => SetProperty(ref showFilterHelp, value);
    }

    public bool HasNextPage => !next.IsEmpty;

    public bool HasPreviousPage => history.Count > 0;

    public string PageDescription => $"Page {history.Count + 1}";

    public DrinkActionViewModel? CreateAction
    {
        get => createAction;
        private set => SetProperty(ref createAction, value);
    }

    public DrinkActionViewModel? EditAction
    {
        get => editAction;
        private set => SetProperty(ref editAction, value);
    }

    public DrinkActionViewModel? DeleteAction
    {
        get => deleteAction;
        private set => SetProperty(ref deleteAction, value);
    }

    public DrinkActionViewModel? TagsAction
    {
        get => tagsAction;
        private set
        {
            if (SetProperty(ref tagsAction, value))
            {
                OnPropertyChanged(nameof(CanEditTags));
                OnPropertyChanged(nameof(CanReplaceTags));
            }
        }
    }

    public string Name
    {
        get => name;
        set => SetFormProperty(ref name, value ?? string.Empty);
    }

    public string Category
    {
        get => category;
        set => SetFormProperty(ref category, value ?? string.Empty);
    }

    public string Glass
    {
        get => glass;
        set => SetFormProperty(ref glass, value ?? string.Empty);
    }

    public string Description
    {
        get => description;
        set => SetFormProperty(ref description, value ?? string.Empty);
    }

    public string Tags
    {
        get => tags;
        set
        {
            if (SetFormProperty(ref tags, value ?? string.Empty) && !updatingForm)
            {
                tagsDirty = true;
            }
        }
    }

    public bool ReplaceTags
    {
        get => replaceTags;
        set => SetFormProperty(ref replaceTags, value);
    }

    public string ExactNameFilter
    {
        get => exactNameFilter;
        set => SetProperty(ref exactNameFilter, value ?? string.Empty);
    }

    public string CategoryFilter
    {
        get => categoryFilter;
        set => SetProperty(ref categoryFilter, value ?? string.Empty);
    }

    public string GlassFilter
    {
        get => glassFilter;
        set => SetProperty(ref glassFilter, value ?? string.Empty);
    }

    public string ExpressionFilter
    {
        get => expressionFilter;
        set => SetProperty(ref expressionFilter, value ?? string.Empty);
    }

    public string PageSize
    {
        get => pageSize;
        set => SetProperty(ref pageSize, value ?? string.Empty);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public Exception? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    public string FilterHelp => "Fields: id, name, category, glass, status, description, tags, recipe.garnish. " +
        "Comparisons: == != < <= > >= in not in. Logic: &&/and ||/or !/not. " +
        "Example: category == \"cocktail\" && name.contains(\"gin\")";

    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() => IsLoading = true, cancellationToken).ConfigureAwait(false);
        ListDrinksRequest request;
        try
        {
            request = BuildListRequest();
        }
        catch (Exception exception)
        {
            await PublishErrorAsync(exception, "build desktop drink list request", cancellationToken).ConfigureAwait(false);
            await dispatcher.InvokeAsync(() => IsLoading = false, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            LatestResult<ListLoadOutcome> latest = await listRequests.RunAsync(
                token => LoadListAsync(request, token),
                cancellationToken).ConfigureAwait(false);
            if (!latest.IsCurrent || latest.Value is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() => PublishList(latest.Value), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested)
        {
            // A newer generation owns the busy state and publication.
        }
    }

    public Task LoadSelectedAsync(
        DrinkListItemViewModel? item,
        CancellationToken cancellationToken = default) => LoadSelectedCoreAsync(item, cancellationToken);

    public async Task ApplyFilterAsync(CancellationToken cancellationToken = default)
    {
        history.Clear();
        currentCursor = default;
        next = default;
        ShowFilterHelp = false;
        OnPagingChanged();
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearFilterAsync(CancellationToken cancellationToken = default)
    {
        ExactNameFilter = string.Empty;
        CategoryFilter = string.Empty;
        GlassFilter = string.Empty;
        ExpressionFilter = string.Empty;
        PageSize = PageRequest.DefaultLimit.ToString(CultureInfo.InvariantCulture);
        await ApplyFilterAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task NextPageAsync(CancellationToken cancellationToken = default)
    {
        if (next.IsEmpty || IsLoading)
        {
            return;
        }

        history.Add(BuildListRequest().Cursor);
        await RefreshWithCursorAsync(next, cancellationToken).ConfigureAwait(false);
    }

    public async Task PreviousPageAsync(CancellationToken cancellationToken = default)
    {
        if (history.Count == 0 || IsLoading)
        {
            return;
        }

        int index = history.Count - 1;
        Cursor cursor = history[index];
        history.RemoveAt(index);
        await RefreshWithCursorAsync(cursor, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled(CreateAction))
        {
            return;
        }

        PopulateForm(null);
        Mode = DrinksDesktopMode.Create;
        await LoadCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StartEditAsync(CancellationToken cancellationToken = default)
    {
        if (Detail is null || !Enabled(EditAction))
        {
            return;
        }

        PopulateForm(Detail);
        Mode = DrinksDesktopMode.Edit;
        await LoadCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    public void BeginDelete()
    {
        if (Detail is not null && Enabled(DeleteAction) && !IsSubmitting)
        {
            Mode = DrinksDesktopMode.DeleteConfirmation;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsForm || Interlocked.CompareExchange(ref submitting, 1, 0) != 0)
        {
            return;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            lifetime.Token,
            cancellationToken);
        Task mutation = SaveCoreAsync(linked.Token);
        TrackMutation(mutation);
        await mutation.ConfigureAwait(false);
    }

    public async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Mode != DrinksDesktopMode.DeleteConfirmation || Detail is null ||
            Interlocked.CompareExchange(ref submitting, 1, 0) != 0)
        {
            return;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            lifetime.Token,
            cancellationToken);
        Task mutation = DeleteCoreAsync(Detail.Id, linked.Token);
        TrackMutation(mutation);
        await mutation.ConfigureAwait(false);
    }

    public void Cancel()
    {
        if (IsSubmitting)
        {
            return;
        }

        SetDirty(false);
        Mode = Detail is null ? DrinksDesktopMode.Browse : DrinksDesktopMode.Detail;
        Error = null;
        StatusMessage = string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        await listRequests.DisposeAsync().ConfigureAwait(false);
        await detailRequests.DisposeAsync().ConfigureAwait(false);
        await catalogRequests.DisposeAsync().ConfigureAwait(false);
        lock (mutationSync)
        {
            pending = mutations.Select(ObserveAsync).ToArray();
        }

        await Task.WhenAll(pending).ConfigureAwait(false);
        lifetime.Dispose();
    }

    private async Task<ListLoadOutcome> LoadListAsync(
        ListDrinksRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            Page<Drink> page = await operations.ListAsync(request, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(null, cancellationToken)
                .ConfigureAwait(false);
            return new ListLoadOutcome(page, projected, null);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ListLoadOutcome(null, [], Safe(exception, "load desktop drinks"));
        }
    }

    private void PublishList(ListLoadOutcome outcome)
    {
        IsLoading = false;
        if (outcome.Error is not null)
        {
            SetError(outcome.Error);
            return;
        }

        DrinkId retained = !pendingSelection.IsEmpty
            ? pendingSelection
            : SelectedItem?.Id ?? default;
        pendingSelection = default;
        Items.Clear();
        foreach (Drink drink in outcome.Page!.Items)
        {
            Items.Add(DrinkListItemViewModel.FromDrink(drink));
        }

        next = outcome.Page.Next;
        PublishActions(outcome.Actions);
        SetError(null);
        SelectedItem = Items.FirstOrDefault(value => value.Id == retained) ?? Items.FirstOrDefault();
        OnPagingChanged();
    }

    private async Task LoadSelectedCoreAsync(
        DrinkListItemViewModel? item,
        CancellationToken cancellationToken)
    {
        try
        {
            LatestResult<DetailLoadOutcome> latest = await detailRequests.RunAsync(
                token => LoadDetailAsync(item, token),
                cancellationToken).ConfigureAwait(false);
            if (!latest.IsCurrent || latest.Value is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() => PublishDetail(latest.Value), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<DetailLoadOutcome> LoadDetailAsync(
        DrinkListItemViewModel? item,
        CancellationToken cancellationToken)
    {
        try
        {
            Drink? loaded = item is null
                ? null
                : await operations.GetAsync(item.Id, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(loaded, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyDictionary<IngredientId, string> ingredientNames = loaded is null
                ? new Dictionary<IngredientId, string>()
                : (await operations.IngredientCatalogAsync(cancellationToken).ConfigureAwait(false))
                    .ToDictionary(static ingredient => ingredient.Id, static ingredient => ingredient.Name);
            IReadOnlyList<DrinkRecipeIngredientDetailViewModel> recipeIngredients = loaded?.Recipe.Ingredients
                .Select(ingredient => new DrinkRecipeIngredientDetailViewModel(
                    ingredientNames.GetValueOrDefault(ingredient.IngredientId, ingredient.IngredientId.Value),
                    ingredient.Amount.ToString(),
                    ingredient.Optional ? "Optional" : "Required"))
                .ToArray() ?? [];
            return new DetailLoadOutcome(loaded, recipeIngredients, projected, null);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DetailLoadOutcome(null, [], [], Safe(exception, "load desktop drink detail"));
        }
    }

    private void PublishDetail(DetailLoadOutcome outcome)
    {
        Detail = outcome.Drink;
        DetailIngredients.Clear();
        foreach (DrinkRecipeIngredientDetailViewModel ingredient in outcome.Ingredients)
        {
            DetailIngredients.Add(ingredient);
        }

        if (Mode is DrinksDesktopMode.Browse or DrinksDesktopMode.Detail)
        {
            Mode = outcome.Drink is null ? DrinksDesktopMode.Browse : DrinksDesktopMode.Detail;
        }

        PublishActions(outcome.Actions);
        SetError(outcome.Error);
    }

    private async Task LoadCatalogAsync(CancellationToken cancellationToken)
    {
        IsCatalogLoading = true;
        try
        {
            LatestResult<CatalogLoadOutcome> latest = await catalogRequests.RunAsync(
                LoadCatalogCoreAsync,
                cancellationToken).ConfigureAwait(false);
            if (!latest.IsCurrent || latest.Value is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                IsCatalogLoading = false;
                if (latest.Value.Error is not null)
                {
                    SetError(latest.Value.Error);
                    return;
                }

                Recipe?.SetCatalog(latest.Value.Options);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<CatalogLoadOutcome> LoadCatalogCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<Ingredient> ingredients = await operations.IngredientCatalogAsync(cancellationToken)
                .ConfigureAwait(false);
            return new CatalogLoadOutcome(
                ingredients.Select(static value => new IngredientOptionViewModel(value.Id, value.Name)).ToArray(),
                null);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CatalogLoadOutcome([], Safe(exception, "load desktop recipe ingredient catalog"));
        }
    }

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dispatcher.InvokeAsync(() => IsSubmitting = true, cancellationToken).ConfigureAwait(false);
            Recipe built = Recipe?.Build()
                ?? throw AppError.FailedPrecondition("drink recipe editor is missing");
            TagCollection? desiredTags = ReplaceTags && tagsDirty ? TagCollection.Parse(Tags) : null;
            Drink saved = Mode switch
            {
                DrinksDesktopMode.Create => await operations.CreateAsync(
                    new CreateDrinkRequest(
                        Name,
                        DrinkCategory.Parse(Category),
                        GlassType.Parse(Glass),
                        built,
                        Description),
                    desiredTags,
                    cancellationToken).ConfigureAwait(false),
                DrinksDesktopMode.Edit when Detail is not null => await operations.UpdateAsync(
                    new UpdateDrinkRequest(
                        Detail.Id,
                        Name,
                        DrinkCategory.Parse(Category),
                        GlassType.Parse(Glass),
                        built,
                        Description,
                        Detail.Revision),
                    desiredTags,
                    cancellationToken).ConfigureAwait(false),
                _ => throw AppError.FailedPrecondition("drink form has no mutation target"),
            };
            pendingSelection = saved.Id;
            await dispatcher.InvokeAsync(() =>
            {
                SetDirty(false);
                tagsDirty = false;
                Mode = DrinksDesktopMode.Browse;
                IsSubmitting = false;
                SetError(null);
            }, cancellationToken).ConfigureAwait(false);
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            await dispatcher.InvokeAsync(() => IsSubmitting = false, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await dispatcher.InvokeAsync(() =>
            {
                IsSubmitting = false;
                SetError(Safe(exception, "save desktop drink"));
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _ = Interlocked.Exchange(ref submitting, 0);
        }
    }

    private async Task DeleteCoreAsync(DrinkId id, CancellationToken cancellationToken)
    {
        try
        {
            await dispatcher.InvokeAsync(() => IsSubmitting = true, cancellationToken).ConfigureAwait(false);
            _ = await operations.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            pendingSelection = default;
            await dispatcher.InvokeAsync(() =>
            {
                Detail = null;
                Mode = DrinksDesktopMode.Browse;
                IsSubmitting = false;
                SetError(null);
            }, cancellationToken).ConfigureAwait(false);
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            await dispatcher.InvokeAsync(() => IsSubmitting = false, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await dispatcher.InvokeAsync(() =>
            {
                IsSubmitting = false;
                SetError(Safe(exception, "delete desktop drink"));
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _ = Interlocked.Exchange(ref submitting, 0);
        }
    }

    private async Task RefreshWithCursorAsync(Cursor cursor, CancellationToken cancellationToken)
    {
        Cursor previous = currentCursor;
        currentCursor = cursor;
        try
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            currentCursor = previous;
            throw;
        }
    }

    private Cursor currentCursor;

    private ListDrinksRequest BuildListRequest()
    {
        if (!int.TryParse(PageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit <= 0)
        {
            throw AppError.Invalid("page size must be greater than zero");
        }

        DrinkCategory? categoryValue = string.IsNullOrWhiteSpace(CategoryFilter)
            ? null
            : DrinkCategory.Parse(CategoryFilter);
        GlassType? glassValue = string.IsNullOrWhiteSpace(GlassFilter)
            ? null
            : GlassType.Parse(GlassFilter);
        return new ListDrinksRequest(
            ExactNameFilter,
            categoryValue,
            glassValue,
            ExpressionFilter,
            currentCursor,
            limit).Normalize();
    }

    private void PopulateForm(Drink? drink)
    {
        updatingForm = true;
        try
        {
            Name = drink?.Name ?? string.Empty;
            Category = drink?.Category.Value ?? DrinkCategory.Cocktail.Value;
            Glass = drink?.Glass.Value ?? GlassType.Rocks.Value;
            Description = drink?.Description ?? string.Empty;
            Tags = drink?.Tags.Format() ?? string.Empty;
            ReplaceTags = true;
            tagsDirty = false;
            Recipe = new DrinkRecipeEditorViewModel(drink?.Recipe, MarkDirty);
            SetDirty(false);
            SetError(null);
        }
        finally
        {
            updatingForm = false;
        }
    }

    private void PublishActions(IReadOnlyList<ActionState> states)
    {
        Actions.Clear();
        foreach (ActionState state in states)
        {
            Actions.Add(new DrinkActionViewModel(state));
        }

        CreateAction = FindAction(DrinkActionProjector.CreateAction);
        EditAction = FindAction(DrinkActionProjector.EditAction);
        DeleteAction = FindAction(DrinkActionProjector.DeleteAction);
        TagsAction = FindAction(DrinkActionProjector.TagsAction);
    }

    private DrinkActionViewModel? FindAction(ActionId id) => Actions.SingleOrDefault(value => value.Id == id);

    private static bool Enabled(DrinkActionViewModel? action) =>
        action is { Visible: true, Enabled: true };

    private void RemoveIngredient(RecipeIngredientViewModel? row)
    {
        try
        {
            if (row is not null)
            {
                Recipe?.RemoveIngredient(row);
            }
        }
        catch (Exception exception)
        {
            SetError(Safe(exception, "remove desktop recipe ingredient"));
        }
    }

    private void RemoveStep(RecipeStepViewModel? row)
    {
        try
        {
            if (row is not null)
            {
                Recipe?.RemoveStep(row);
            }
        }
        catch (Exception exception)
        {
            SetError(Safe(exception, "remove desktop recipe step"));
        }
    }

    private void MarkDirty()
    {
        if (!updatingForm && IsForm)
        {
            SetDirty(true);
        }
    }

    private void SetDirty(bool value)
    {
        if (isDirty == value)
        {
            return;
        }

        isDirty = value;
        OnPropertyChanged(nameof(IsDirty));
    }

    private bool SetFormProperty<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value))
        {
            return false;
        }

        MarkDirty();
        return true;
    }

    private void SetError(Exception? value)
    {
        Error = value;
        StatusMessage = AppError.Find(value)?.UserMessage ?? string.Empty;
    }

    private async Task PublishErrorAsync(
        Exception exception,
        string operation,
        CancellationToken cancellationToken) => await dispatcher.InvokeAsync(
            () => SetError(Safe(exception, operation)),
            cancellationToken).ConfigureAwait(false);

    private void OnPagingChanged()
    {
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(PageDescription));
    }

    private void TrackMutation(Task task)
    {
        lock (mutationSync)
        {
            _ = mutations.Add(task);
        }

        _ = task.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                lock (mutationSync)
                {
                    _ = mutations.Remove(completed);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed record ListLoadOutcome(
        Page<Drink>? Page,
        IReadOnlyList<ActionState> Actions,
        Exception? Error);

    private sealed record DetailLoadOutcome(
        Drink? Drink,
        IReadOnlyList<DrinkRecipeIngredientDetailViewModel> Ingredients,
        IReadOnlyList<ActionState> Actions,
        Exception? Error);

    private sealed record CatalogLoadOutcome(
        IReadOnlyList<IngredientOptionViewModel> Options,
        Exception? Error);
}
