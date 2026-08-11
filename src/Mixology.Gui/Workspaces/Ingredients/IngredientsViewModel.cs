using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Gui.Workspaces.Ingredients;

public enum IngredientEditorMode
{
    Browse,
    Create,
    Edit,
    Retire,
}

public sealed record IngredientListItemViewModel(Ingredient Value)
{
    public IngredientId Id => Value.Id;
    public string Name => Value.Name;
    public string Category => Value.Category.Value;
    public string Unit => Value.Unit.Value;
}

public interface IIngredientsDesktopOperations
{
    Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken cancellationToken);
    Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Ingredient? selected, CancellationToken cancellationToken);
    Task<Ingredient> CreateAsync(CreateIngredientRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Ingredient> UpdateAsync(UpdateIngredientRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Ingredient> RetireAsync(RetireIngredientRequest request, CancellationToken cancellationToken);
}

public sealed partial class IngredientsViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly IIngredientsDesktopOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<IngredientPageOutcome> pages = new();
    private readonly LatestRequest<IngredientDetailOutcome> details = new();
    private readonly LatestRequest<IngredientMutationOutcome> mutations = new();
    private readonly List<Cursor> history = [];
    private ListIngredientsRequest request = new();
    private Cursor next;
    private bool disposed;
    private bool suppressDirty;

    public IngredientsViewModel(IIngredientsDesktopOperations operations, IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = Command(RefreshAsync);
        ApplyFilterCommand = Command(ApplyFilterAsync);
        NextPageCommand = Command(NextPageAsync);
        PreviousPageCommand = Command(PreviousPageAsync);
        BeginCreateCommand = new RelayCommand(BeginCreate);
        BeginEditCommand = new RelayCommand(BeginEdit);
        BeginRetireCommand = new RelayCommand(BeginRetire);
        CancelEditorCommand = new RelayCommand(CancelEditor);
        SubmitCommand = Command(SubmitAsync);
        ToggleFilterHelpCommand = new RelayCommand(() => ShowFilterHelp = !ShowFilterHelp);
    }

    public WorkspaceId Id => NavigationProjector.IngredientsWorkspace;
    public string Title => "Ingredients";
    public bool IsDirty => EditorMode != IngredientEditorMode.Browse && EditorDirty;
    public ObservableCollection<IngredientListItemViewModel> Items { get; } = [];
    public IReadOnlyList<string> Categories { get; } = IngredientCategory.All.Select(static value => value.Value).ToArray();
    public IReadOnlyList<string> Units { get; } = Unit.All.Select(static value => value.Value).ToArray();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ApplyFilterCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IRelayCommand BeginCreateCommand { get; }
    public IRelayCommand BeginEditCommand { get; }
    public IRelayCommand BeginRetireCommand { get; }
    public IRelayCommand CancelEditorCommand { get; }
    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand ToggleFilterHelpCommand { get; }

    public const string FilterHelp = "Fields: id, name, category, unit, description, tags\n" +
        "Comparisons: == != < <= > >= in not in\n" +
        "Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches\n\n" +
        "category == \"spirit\" && name.contains(\"gin\")\n" +
        "unit in [\"ml\", \"oz\"] && !description.contains(\"seasonal\")\n" +
        "tags contains \"featured\" || tags contains \"region=west\"";

    [ObservableProperty]
    public partial IngredientListItemViewModel? SelectedItem { get; set; }
    [ObservableProperty]
    public partial Ingredient? SelectedIngredient { get; set; }
    [ObservableProperty]
    public partial IngredientEditorMode EditorMode { get; set; }
    [ObservableProperty]
    public partial bool IsLoading { get; set; }
    [ObservableProperty]
    public partial bool IsSubmitting { get; set; }
    [ObservableProperty]
    public partial bool ShowFilterHelp { get; set; }
    [ObservableProperty]
    public partial string FilterExpression { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string CategoryFilter { get; set; } = string.Empty;
    [ObservableProperty]
    public partial int PageSize { get; set; } = PageRequest.DefaultLimit;
    [ObservableProperty]
    public partial int PageNumber { get; set; } = 1;
    [ObservableProperty]
    public partial bool HasNextPage { get; set; }
    [ObservableProperty]
    public partial bool HasPreviousPage { get; set; }
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string EditorName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string EditorCategory { get; set; } = IngredientCategory.Other.Value;
    [ObservableProperty]
    public partial string EditorUnit { get; set; } = Unit.Ounce.Value;
    [ObservableProperty]
    public partial string EditorDescription { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool ReplaceTags { get; set; }
    [ObservableProperty]
    public partial string EditorTags { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ReplacementIngredientId { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ReplacementRatio { get; set; } = "1";
    [ObservableProperty]
    public partial bool EditorDirty { get; set; }
    [ObservableProperty]
    public partial bool CanCreate { get; set; }
    [ObservableProperty]
    public partial bool CanEdit { get; set; }
    [ObservableProperty]
    public partial bool CanRetire { get; set; }
    [ObservableProperty]
    public partial bool CanReplaceTags { get; set; }
    [ObservableProperty]
    public partial bool IsCreateVisible { get; set; }
    [ObservableProperty]
    public partial bool IsEditVisible { get; set; }
    [ObservableProperty]
    public partial bool IsRetireVisible { get; set; }
    [ObservableProperty]
    public partial bool IsTagsVisible { get; set; }
    [ObservableProperty]
    public partial string CreateDisabledReason { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string EditDisabledReason { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string RetireDisabledReason { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string TagsDisabledReason { get; set; } = string.Empty;

    public Exception? Error { get; private set; }
    public bool IsEditorVisible => EditorMode != IngredientEditorMode.Browse;
    public bool IsRetireEditor => EditorMode == IngredientEditorMode.Retire;
    public bool IsIngredientEditor => EditorMode is IngredientEditorMode.Create or IngredientEditorMode.Edit;

    public static Func<IDesktopWorkspace> CreateFactory(
        IngredientsModule ingredients,
        IngredientActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new IngredientsViewModel(
            new ModuleOperations(ingredients, projector, taggedMutations, session, actor), dispatcher);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) => RefreshAsync(cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() => IsLoading = true, cancellationToken).ConfigureAwait(false);
        try
        {
            ListIngredientsRequest snapshot = request;
            LatestResult<IngredientPageOutcome> latest = await pages.RunAsync(
                token => LoadPageAsync(snapshot, token), cancellationToken).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is { } outcome)
            {
                await dispatcher.InvokeAsync(() => PublishPage(outcome), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await pages.DisposeAsync().ConfigureAwait(false);
        await details.DisposeAsync().ConfigureAwait(false);
        await mutations.DisposeAsync().ConfigureAwait(false);
    }

    partial void OnSelectedItemChanged(IngredientListItemViewModel? value)
    {
        if (!disposed) _ = LoadDetailAsync(value);
    }

    partial void OnEditorModeChanged(IngredientEditorMode value)
    {
        OnPropertyChanged(nameof(IsEditorVisible));
        OnPropertyChanged(nameof(IsRetireEditor));
        OnPropertyChanged(nameof(IsIngredientEditor));
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnEditorDirtyChanged(bool value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnEditorNameChanged(string value) => MarkDirty();
    partial void OnEditorCategoryChanged(string value) => MarkDirty();
    partial void OnEditorUnitChanged(string value) => MarkDirty();
    partial void OnEditorDescriptionChanged(string value) => MarkDirty();
    partial void OnReplaceTagsChanged(bool value) => MarkDirty();
    partial void OnEditorTagsChanged(string value) => MarkDirty();
    partial void OnReplacementIngredientIdChanged(string value) => MarkDirty();
    partial void OnReplacementRatioChanged(string value) => MarkDirty();

    private static AsyncRelayCommand Command(Func<CancellationToken, Task> execute) =>
        new(execute, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

    private async Task<IngredientPageOutcome> LoadPageAsync(ListIngredientsRequest snapshot, CancellationToken token)
    {
        try
        {
            Page<Ingredient> page = await operations.ListAsync(snapshot, token).ConfigureAwait(false);
            IReadOnlyList<ActionState> actions = await operations.ProjectAsync(null, token).ConfigureAwait(false);
            return new(page, actions, null);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
        catch (Exception exception) { return new(new Page<Ingredient>([], default), [], Safe(exception, "load desktop ingredients")); }
    }

    private void PublishPage(IngredientPageOutcome outcome)
    {
        IngredientId? selected = SelectedItem?.Id;
        Items.Clear();
        foreach (Ingredient ingredient in outcome.Page.Items)
        {
            Items.Add(new(ingredient));
        }

        // Publish collection-level capabilities first. Selecting the row starts a detail
        // projection synchronously, and its resource-specific result must be the last writer.
        PublishActions(outcome.Actions);
        SelectedItem = selected is { } id ? Items.FirstOrDefault(item => item.Id == id) : Items.FirstOrDefault();
        next = outcome.Page.Next;
        HasNextPage = !next.IsEmpty;
        HasPreviousPage = history.Count > 0;
        PageNumber = history.Count + 1;
        PublishError(outcome.Error);
        IsLoading = false;
    }

    private async Task LoadDetailAsync(IngredientListItemViewModel? selected)
    {
        try
        {
            LatestResult<IngredientDetailOutcome> latest = await details.RunAsync(async token =>
            {
                try
                {
                    Ingredient? detail = selected is null ? null : await operations.GetAsync(selected.Id, token).ConfigureAwait(false);
                    IReadOnlyList<ActionState> actions = await operations.ProjectAsync(detail, token).ConfigureAwait(false);
                    return new(detail, actions, null);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return new(null, [], Safe(exception, "load desktop ingredient detail")); }
            }).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is { } outcome)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    SelectedIngredient = outcome.Ingredient;
                    PublishActions(outcome.Actions);
                    PublishError(outcome.Error);
                }).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
    }

    private async Task ApplyFilterAsync(CancellationToken token)
    {
        IngredientCategory? category = string.IsNullOrWhiteSpace(CategoryFilter)
            ? null : IngredientCategory.Parse(CategoryFilter);
        request = new ListIngredientsRequest(category, FilterExpression, default, PageSize).Normalize();
        history.Clear();
        await RefreshAsync(token).ConfigureAwait(false);
    }

    private async Task NextPageAsync(CancellationToken token)
    {
        if (next.IsEmpty)
        {
            return;
        }

        history.Add(request.Cursor);
        request = request with { Cursor = next };
        await RefreshAsync(token).ConfigureAwait(false);
    }

    private async Task PreviousPageAsync(CancellationToken token)
    {
        if (history.Count == 0)
        {
            return;
        }

        int index = history.Count - 1;
        request = request with { Cursor = history[index] };
        history.RemoveAt(index);
        await RefreshAsync(token).ConfigureAwait(false);
    }

    private void BeginCreate()
    {
        if (!CanCreate)
        {
            return;
        }

        PopulateEditor(null, IngredientEditorMode.Create);
        // The created resource does not have an identity to project yet. The atomic coordinator
        // performs the authoritative tag decision against its post-create Cedar resource.
        IsTagsVisible = true;
        CanReplaceTags = true;
        TagsDisabledReason = string.Empty;
    }

    private void BeginEdit()
    {
        if (!CanEdit || SelectedIngredient is null)
        {
            return;
        }

        PopulateEditor(SelectedIngredient, IngredientEditorMode.Edit);
    }

    private void BeginRetire()
    {
        if (!CanRetire || SelectedIngredient is null)
        {
            return;
        }

        suppressDirty = true;
        ReplacementIngredientId = string.Empty;
        ReplacementRatio = "1";
        EditorMode = IngredientEditorMode.Retire;
        EditorDirty = false;
        suppressDirty = false;
    }

    private void PopulateEditor(Ingredient? ingredient, IngredientEditorMode mode)
    {
        suppressDirty = true;
        EditorName = ingredient?.Name ?? string.Empty;
        EditorCategory = ingredient?.Category.Value ?? IngredientCategory.Other.Value;
        EditorUnit = ingredient?.Unit.Value ?? Unit.Ounce.Value;
        EditorDescription = ingredient?.Description ?? string.Empty;
        EditorTags = ingredient?.Tags.Format() ?? string.Empty;
        ReplaceTags = false;
        EditorMode = mode;
        EditorDirty = false;
        suppressDirty = false;
    }

    private void CancelEditor()
    {
        EditorMode = IngredientEditorMode.Browse;
        EditorDirty = false;
        PublishError(null);
    }

    public async Task SubmitAsync(CancellationToken token = default)
    {
        if (IsSubmitting || EditorMode == IngredientEditorMode.Browse)
        {
            return;
        }

        IsSubmitting = true;
        IngredientEditorMode mode = EditorMode;
        try
        {
            TagCollection? tags = ReplaceTags ? TagCollection.Parse(EditorTags) : null;
            Func<CancellationToken, Task<Ingredient>> mutate = mode switch
            {
                IngredientEditorMode.Create => ct => operations.CreateAsync(
                    new CreateIngredientRequest(EditorName, IngredientCategory.Parse(EditorCategory), Unit.Parse(EditorUnit), EditorDescription).Normalize(), tags, ct),
                IngredientEditorMode.Edit when SelectedIngredient is { } selected => ct => operations.UpdateAsync(
                    new UpdateIngredientRequest(selected.Id, EditorName, IngredientCategory.Parse(EditorCategory), Unit.Parse(EditorUnit), EditorDescription, selected.Revision).Normalize(), tags, ct),
                IngredientEditorMode.Retire when SelectedIngredient is { } selected => ct => operations.RetireAsync(
                    new RetireIngredientRequest(selected.Id, ParseRetirement()).Normalize(), ct),
                _ => throw AppError.FailedPrecondition("ingredient editor has no target"),
            };
            LatestResult<IngredientMutationOutcome> latest = await mutations.RunAsync(async ct =>
            {
                try { return new(await mutate(ct).ConfigureAwait(false), null); }
                catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
                catch (Exception exception) { return new(null, Safe(exception, "mutate desktop ingredient")); }
            }, token).ConfigureAwait(false);
            if (!latest.IsCurrent || latest.Value is not { } outcome)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                PublishError(outcome.Error);
                if (outcome.Error is null)
                {
                    EditorMode = IngredientEditorMode.Browse;
                    EditorDirty = false;
                }
                IsSubmitting = false;
            }, token).ConfigureAwait(false);
            if (outcome.Error is null)
            {
                await RefreshAsync(token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            await dispatcher.InvokeAsync(() => IsSubmitting = false, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await dispatcher.InvokeAsync(() => { PublishError(Safe(exception, "mutate desktop ingredient")); IsSubmitting = false; }, token)
                .ConfigureAwait(false);
        }
    }

    private Retirement ParseRetirement()
    {
        if (string.IsNullOrWhiteSpace(ReplacementIngredientId))
        {
            return new Retirement();
        }

        if (!double.TryParse(ReplacementRatio, NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio))
        {
            throw AppError.Invalid("replacement ratio must be a number");
        }

        return new Retirement(IngredientId.Parse(ReplacementIngredientId.Trim()), ratio).Normalize();
    }

    private void PublishActions(IReadOnlyList<ActionState> values)
    {
        Dictionary<ActionId, ActionState> states = values.ToDictionary(static state => state.Id);
        Apply(IngredientActionProjector.CreateAction, out bool createVisible, out bool create, out string createReason);
        Apply(IngredientActionProjector.EditAction, out bool editVisible, out bool edit, out string editReason);
        Apply(IngredientActionProjector.RetireAction, out bool retireVisible, out bool retire, out string retireReason);
        Apply(IngredientActionProjector.TagsAction, out bool tagsVisible, out bool tags, out string tagsReason);
        IsCreateVisible = createVisible;
        CanCreate = create; CreateDisabledReason = createReason;
        IsEditVisible = editVisible;
        CanEdit = edit; EditDisabledReason = editReason;
        IsRetireVisible = retireVisible;
        CanRetire = retire; RetireDisabledReason = retireReason;
        IsTagsVisible = tagsVisible;
        CanReplaceTags = tags; TagsDisabledReason = tagsReason;
        return;

        void Apply(ActionId id, out bool visible, out bool enabled, out string reason)
        {
            if (!states.TryGetValue(id, out ActionState? state) || !state.Visible)
            {
                visible = false;
                enabled = false;
                reason = string.Empty;
                return;
            }

            visible = true;
            enabled = state.Enabled;
            reason = state.Enabled ? string.Empty : state.DisabledReason;
        }
    }

    private void PublishError(Exception? error)
    {
        Error = error;
        StatusMessage = AppError.Find(error)?.UserMessage ?? string.Empty;
        OnPropertyChanged(nameof(Error));
    }

    private void MarkDirty()
    {
        if (!suppressDirty && EditorMode != IngredientEditorMode.Browse)
        {
            EditorDirty = true;
        }
    }

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) ?? (AppError.IsCancellation(exception) ? exception : AppError.Internal(operation, exception));

    private sealed record IngredientPageOutcome(Page<Ingredient> Page, IReadOnlyList<ActionState> Actions, Exception? Error);
    private sealed record IngredientDetailOutcome(Ingredient? Ingredient, IReadOnlyList<ActionState> Actions, Exception? Error);
    private sealed record IngredientMutationOutcome(Ingredient? Ingredient, Exception? Error);

    private sealed class ModuleOperations(
        IngredientsModule ingredients,
        IngredientActionProjector projector,
        TaggedMutationCoordinator mutations,
        MixologySession session,
        Actor actor) : IIngredientsDesktopOperations
    {
        public Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken token) =>
            ingredients.ListAsync(session, request, token);
        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken token) => ingredients.GetAsync(session, id, token);
        public Task<IReadOnlyList<ActionState>> ProjectAsync(Ingredient? selected, CancellationToken token) =>
            projector.ProjectAsync(actor, selected, token);
        public Task<Ingredient> CreateAsync(CreateIngredientRequest request, TagCollection? tags, CancellationToken token) =>
            mutations.RunAsync(session, (active, ct) => ingredients.CreateAsync(active, request, ct), tags,
                static value => value.EntityUid, static (value, updated) => value with { Tags = updated }, token);
        public Task<Ingredient> UpdateAsync(UpdateIngredientRequest request, TagCollection? tags, CancellationToken token) =>
            mutations.RunAsync(session, (active, ct) => ingredients.UpdateAsync(active, request, ct), tags,
                static value => value.EntityUid, static (value, updated) => value with { Tags = updated }, token);
        public Task<Ingredient> RetireAsync(RetireIngredientRequest request, CancellationToken token) =>
            ingredients.RetireAsync(session, request, token);
    }
}
