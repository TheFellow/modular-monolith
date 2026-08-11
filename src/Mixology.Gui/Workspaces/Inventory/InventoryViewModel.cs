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
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Inventory.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Gui.Workspaces.Inventory;

public enum InventoryEditorMode
{
    Browse,
    Adjust,
    Set,
}

public sealed record InventoryListItemViewModel(InventoryStock Stock, Ingredient Ingredient)
{
    public InventoryId Id => Stock.Id;
    public string Name => Ingredient.Name;
    public string Category => Ingredient.Category.Value;
    public string OnHand => Stock.OnHand.ToString();
    public string Available => Stock.Available.ToString();
    public string Cost => Stock.UnitCost?.ToString() ?? "N/A";
}

public interface IInventoryDesktopOperations
{
    Task<Page<InventoryListItemViewModel>> ListAsync(ListInventoryRequest request, CancellationToken cancellationToken);
    Task<InventoryListItemViewModel> GetAsync(IngredientId ingredientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(InventoryStock? selected, CancellationToken cancellationToken);
    Task<InventoryStock> AdjustAsync(AdjustInventoryRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<InventoryStock> SetAsync(SetInventoryRequest request, TagCollection? tags, CancellationToken cancellationToken);
}

public sealed partial class InventoryViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly IInventoryDesktopOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<InventoryPageOutcome> pages = new();
    private readonly LatestRequest<InventoryDetailOutcome> details = new();
    private readonly LatestRequest<InventoryMutationOutcome> mutations = new();
    private readonly List<Cursor> history = [];
    private ListInventoryRequest request = new();
    private Cursor next;
    private bool disposed;
    private bool suppressDirty;

    public InventoryViewModel(IInventoryDesktopOperations operations, IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = Command(RefreshAsync);
        ApplyFilterCommand = Command(ApplyFilterAsync);
        NextPageCommand = Command(NextPageAsync);
        PreviousPageCommand = Command(PreviousPageAsync);
        BeginAdjustCommand = new RelayCommand(BeginAdjust);
        BeginSetCommand = new RelayCommand(BeginSet);
        CancelEditorCommand = new RelayCommand(CancelEditor);
        SubmitCommand = Command(SubmitAsync);
        ToggleFilterHelpCommand = new RelayCommand(() => ShowFilterHelp = !ShowFilterHelp);
    }

    public WorkspaceId Id => NavigationProjector.InventoryWorkspace;
    public string Title => "Inventory";
    public bool IsDirty => EditorMode != InventoryEditorMode.Browse && EditorDirty;
    public bool IsEditorVisible => EditorMode != InventoryEditorMode.Browse;
    public bool IsAdjustEditor => EditorMode == InventoryEditorMode.Adjust;
    public bool IsSetEditor => EditorMode == InventoryEditorMode.Set;
    public ObservableCollection<InventoryListItemViewModel> Items { get; } = [];
    public IReadOnlyList<string> Units { get; } = Unit.All.Select(static value => value.Value).ToArray();
    public IReadOnlyList<string> Reasons { get; } = AdjustmentReason.All.Select(static value => value.Value).ToArray();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ApplyFilterCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IRelayCommand BeginAdjustCommand { get; }
    public IRelayCommand BeginSetCommand { get; }
    public IRelayCommand CancelEditorCommand { get; }
    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand ToggleFilterHelpCommand { get; }

    public const string FilterHelp = "Fields: id, ingredient_id, quantity, unit, last_updated, tags\n" +
        "Comparisons: == != < <= > >= in not in\n" +
        "Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches\n\n" +
        "quantity <= 5 && unit == \"ml\"\n" +
        "ingredient_id.startsWith(\"ing-\") || quantity == 0\n" +
        "tags contains \"featured\" || tags contains \"region=west\"";

    [ObservableProperty]
    public partial InventoryListItemViewModel? SelectedItem { get; set; }
    [ObservableProperty]
    public partial InventoryListItemViewModel? SelectedInventory { get; set; }
    [ObservableProperty]
    public partial InventoryEditorMode EditorMode { get; set; }
    [ObservableProperty]
    public partial bool IsLoading { get; set; }
    [ObservableProperty]
    public partial bool IsSubmitting { get; set; }
    [ObservableProperty]
    public partial bool ShowFilterHelp { get; set; }
    [ObservableProperty]
    public partial string FilterExpression { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool LowStockOnly { get; set; }
    [ObservableProperty]
    public partial double LowStockThreshold { get; set; } = ListInventoryRequest.DefaultLowStockThreshold;
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
    public partial string EditorQuantity { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string EditorDelta { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string EditorUnit { get; set; } = Unit.Ounce.Value;
    [ObservableProperty]
    public partial string EditorCost { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string EditorReason { get; set; } = AdjustmentReason.Received.Value;
    [ObservableProperty]
    public partial bool ReplaceTags { get; set; }
    [ObservableProperty]
    public partial string EditorTags { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool EditorDirty { get; set; }
    [ObservableProperty]
    public partial bool CanAdjust { get; set; }
    [ObservableProperty]
    public partial bool CanSet { get; set; }
    [ObservableProperty]
    public partial bool CanReplaceTags { get; set; }
    [ObservableProperty]
    public partial bool IsAdjustVisible { get; set; }
    [ObservableProperty]
    public partial bool IsSetVisible { get; set; }
    [ObservableProperty]
    public partial bool IsTagsVisible { get; set; }
    [ObservableProperty]
    public partial string AdjustDisabledReason { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string SetDisabledReason { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string TagsDisabledReason { get; set; } = string.Empty;

    public Exception? Error { get; private set; }

    public static Func<IDesktopWorkspace> CreateFactory(
        InventoryModule inventory,
        IngredientsModule ingredients,
        InventoryActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new InventoryViewModel(
            new ModuleOperations(inventory, ingredients, projector, taggedMutations, session, actor), dispatcher);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) => RefreshAsync(cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() => IsLoading = true, cancellationToken).ConfigureAwait(false);
        try
        {
            ListInventoryRequest snapshot = request;
            LatestResult<InventoryPageOutcome> latest = await pages.RunAsync(
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

    partial void OnSelectedItemChanged(InventoryListItemViewModel? value)
    {
        if (!disposed)
        {
            _ = LoadDetailAsync(value);
        }
    }

    partial void OnEditorModeChanged(InventoryEditorMode value)
    {
        OnPropertyChanged(nameof(IsEditorVisible));
        OnPropertyChanged(nameof(IsAdjustEditor));
        OnPropertyChanged(nameof(IsSetEditor));
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnEditorDirtyChanged(bool value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnEditorQuantityChanged(string value) => MarkDirty();
    partial void OnEditorDeltaChanged(string value) => MarkDirty();
    partial void OnEditorUnitChanged(string value) => MarkDirty();
    partial void OnEditorCostChanged(string value) => MarkDirty();
    partial void OnEditorReasonChanged(string value) => MarkDirty();
    partial void OnReplaceTagsChanged(bool value) => MarkDirty();
    partial void OnEditorTagsChanged(string value) => MarkDirty();

    private static AsyncRelayCommand Command(Func<CancellationToken, Task> execute) =>
        new(execute, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

    private async Task<InventoryPageOutcome> LoadPageAsync(ListInventoryRequest snapshot, CancellationToken token)
    {
        try
        {
            Page<InventoryListItemViewModel> page = await operations.ListAsync(snapshot, token).ConfigureAwait(false);
            IReadOnlyList<ActionState> actions = await operations.ProjectAsync(null, token).ConfigureAwait(false);
            return new(page, actions, null);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(new Page<InventoryListItemViewModel>([], default), [], Safe(exception, "load desktop inventory"));
        }
    }

    private void PublishPage(InventoryPageOutcome outcome)
    {
        InventoryId? selected = SelectedItem?.Id;
        Items.Clear();
        foreach (InventoryListItemViewModel item in outcome.Page.Items)
        {
            Items.Add(item);
        }

        // Detail selection can complete synchronously in tests and fast local databases; its
        // resource projection must win over the collection-level capability projection.
        PublishActions(outcome.Actions);
        SelectedItem = selected is { } id ? Items.FirstOrDefault(item => item.Id == id) : Items.FirstOrDefault();
        next = outcome.Page.Next;
        HasNextPage = !next.IsEmpty;
        HasPreviousPage = history.Count > 0;
        PageNumber = history.Count + 1;
        PublishError(outcome.Error);
        IsLoading = false;
    }

    private async Task LoadDetailAsync(InventoryListItemViewModel? selected)
    {
        try
        {
            LatestResult<InventoryDetailOutcome> latest = await details.RunAsync(async token =>
            {
                try
                {
                    InventoryListItemViewModel? detail = selected is null
                        ? null
                        : await operations.GetAsync(selected.Stock.IngredientId, token).ConfigureAwait(false);
                    IReadOnlyList<ActionState> actions = await operations.ProjectAsync(detail?.Stock, token).ConfigureAwait(false);
                    return new(detail, actions, null);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception))
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return new(null, [], Safe(exception, "load desktop inventory detail"));
                }
            }).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is { } outcome)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    SelectedInventory = outcome.Inventory;
                    PublishActions(outcome.Actions);
                    PublishError(outcome.Error);
                }).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
        }
    }

    private async Task ApplyFilterAsync(CancellationToken token)
    {
        request = new ListInventoryRequest(
            LowStock: LowStockOnly ? LowStockThreshold : null,
            Filter: FilterExpression,
            Limit: PageSize).Normalize();
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

    private void BeginAdjust()
    {
        if (!CanAdjust || SelectedInventory is null)
        {
            return;
        }

        PopulateEditor(InventoryEditorMode.Adjust);
        EditorDelta = string.Empty;
        EditorReason = AdjustmentReason.Received.Value;
        EditorDirty = false;
    }

    private void BeginSet()
    {
        if (!CanSet || SelectedInventory is null)
        {
            return;
        }

        PopulateEditor(InventoryEditorMode.Set);
        EditorQuantity = SelectedInventory.Stock.OnHand.Value.ToString(CultureInfo.InvariantCulture);
        EditorDirty = false;
    }

    private void PopulateEditor(InventoryEditorMode mode)
    {
        InventoryListItemViewModel selected = SelectedInventory!;
        suppressDirty = true;
        EditorUnit = selected.Stock.OnHand.Unit.Value;
        EditorCost = selected.Stock.UnitCost?.ToString() ?? string.Empty;
        EditorTags = selected.Stock.Tags.Format();
        ReplaceTags = false;
        EditorMode = mode;
        EditorDirty = false;
        suppressDirty = false;
    }

    private void CancelEditor()
    {
        EditorMode = InventoryEditorMode.Browse;
        EditorDirty = false;
        PublishError(null);
    }

    public async Task SubmitAsync(CancellationToken token = default)
    {
        if (IsSubmitting || EditorMode == InventoryEditorMode.Browse || SelectedInventory is null)
        {
            return;
        }

        IsSubmitting = true;
        InventoryEditorMode mode = EditorMode;
        try
        {
            TagCollection? tags = ReplaceTags ? TagCollection.Parse(EditorTags) : null;
            IngredientId ingredientId = SelectedInventory.Stock.IngredientId;
            Func<CancellationToken, Task<InventoryStock>> mutate = mode switch
            {
                InventoryEditorMode.Adjust => ct => operations.AdjustAsync(ParseAdjustment(ingredientId), tags, ct),
                InventoryEditorMode.Set => ct => operations.SetAsync(ParseSet(ingredientId), tags, ct),
                _ => throw AppError.FailedPrecondition("inventory editor has no target"),
            };
            LatestResult<InventoryMutationOutcome> latest = await mutations.RunAsync(async ct =>
            {
                try
                {
                    return new(await mutate(ct).ConfigureAwait(false), null);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception))
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return new(null, Safe(exception, "mutate desktop inventory"));
                }
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
                    EditorMode = InventoryEditorMode.Browse;
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
            await dispatcher.InvokeAsync(() =>
            {
                PublishError(Safe(exception, "mutate desktop inventory"));
                IsSubmitting = false;
            }, token).ConfigureAwait(false);
        }
    }

    private AdjustInventoryRequest ParseAdjustment(IngredientId ingredientId)
    {
        Amount? delta = string.IsNullOrWhiteSpace(EditorDelta)
            ? null
            : Amount.Create(ParseDouble(EditorDelta, "delta"), Unit.Parse(EditorUnit));
        Price? cost = string.IsNullOrWhiteSpace(EditorCost) ? null : Price.Parse(EditorCost);
        return new AdjustInventoryRequest(
            ingredientId,
            AdjustmentReason.Parse(EditorReason),
            delta,
            cost).Normalize();
    }

    private SetInventoryRequest ParseSet(IngredientId ingredientId)
    {
        Amount quantity = Amount.Create(ParseDouble(EditorQuantity, "quantity"), Unit.Parse(EditorUnit));
        Price cost = string.IsNullOrWhiteSpace(EditorCost)
            ? SelectedInventory?.Stock.UnitCost ?? new Price(0m, Currency.Usd)
            : Price.Parse(EditorCost);
        return new SetInventoryRequest(
            ingredientId,
            quantity,
            cost,
            SelectedInventory?.Stock.Revision ?? 0).Normalize();
    }

    private static double ParseDouble(string raw, string name) =>
        double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw AppError.Invalid($"{name} must be a number");

    private void PublishActions(IReadOnlyList<ActionState> values)
    {
        Dictionary<ActionId, ActionState> states = values.ToDictionary(static state => state.Id);
        Apply(InventoryActionProjector.AdjustAction, out bool adjustVisible, out bool adjust, out string adjustReason);
        Apply(InventoryActionProjector.SetAction, out bool setVisible, out bool set, out string setReason);
        Apply(InventoryActionProjector.TagsAction, out bool tagsVisible, out bool tags, out string tagsReason);
        IsAdjustVisible = adjustVisible;
        CanAdjust = adjust;
        AdjustDisabledReason = adjustReason;
        IsSetVisible = setVisible;
        CanSet = set;
        SetDisabledReason = setReason;
        IsTagsVisible = tagsVisible;
        CanReplaceTags = tags;
        TagsDisabledReason = tagsReason;
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
        if (!suppressDirty && EditorMode != InventoryEditorMode.Browse)
        {
            EditorDirty = true;
        }
    }

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) ?? (AppError.IsCancellation(exception) ? exception : AppError.Internal(operation, exception));

    private sealed record InventoryPageOutcome(
        Page<InventoryListItemViewModel> Page,
        IReadOnlyList<ActionState> Actions,
        Exception? Error);
    private sealed record InventoryDetailOutcome(
        InventoryListItemViewModel? Inventory,
        IReadOnlyList<ActionState> Actions,
        Exception? Error);
    private sealed record InventoryMutationOutcome(InventoryStock? Inventory, Exception? Error);

    private sealed class ModuleOperations(
        InventoryModule inventory,
        IngredientsModule ingredients,
        InventoryActionProjector projector,
        TaggedMutationCoordinator mutations,
        MixologySession session,
        Actor actor) : IInventoryDesktopOperations
    {
        public async Task<Page<InventoryListItemViewModel>> ListAsync(
            ListInventoryRequest request,
            CancellationToken token)
        {
            Page<InventoryStock> page = await inventory.ListAsync(session, request, token).ConfigureAwait(false);
            List<InventoryListItemViewModel> rows = [];
            foreach (InventoryStock stock in page.Items)
            {
                rows.Add(new(stock, await RequireIngredientAsync(stock.IngredientId, token).ConfigureAwait(false)));
            }

            return new Page<InventoryListItemViewModel>(rows, page.Next);
        }

        public async Task<InventoryListItemViewModel> GetAsync(IngredientId id, CancellationToken token)
        {
            InventoryStock stock = await inventory.GetAsync(session, id, token).ConfigureAwait(false);
            return new(stock, await RequireIngredientAsync(id, token).ConfigureAwait(false));
        }

        public Task<IReadOnlyList<ActionState>> ProjectAsync(InventoryStock? selected, CancellationToken token) =>
            projector.ProjectAsync(actor, selected, token);

        public Task<InventoryStock> AdjustAsync(AdjustInventoryRequest request, TagCollection? tags, CancellationToken token) =>
            mutations.RunAsync(session, (active, ct) => inventory.AdjustAsync(active, request, ct), tags,
                static value => value.EntityUid, static (value, updated) => value with { Tags = updated }, token);

        public Task<InventoryStock> SetAsync(SetInventoryRequest request, TagCollection? tags, CancellationToken token) =>
            mutations.RunAsync(session, (active, ct) => inventory.SetAsync(active, request, ct), tags,
                static value => value.EntityUid, static (value, updated) => value with { Tags = updated }, token);

        private async Task<Ingredient> RequireIngredientAsync(IngredientId id, CancellationToken token)
        {
            try
            {
                return await ingredients.GetAsync(session, id, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (!AppError.IsCancellation(exception) && AppError.IsNotFound(exception))
            {
                throw AppError.Internal($"inventory ingredient {id} is missing", exception);
            }
        }
    }
}
