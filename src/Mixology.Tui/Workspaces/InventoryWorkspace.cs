using System.Globalization;
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
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;

namespace Mixology.Tui.Workspaces;

public enum InventoryWorkspaceMode
{
    Browse,
    Filter,
    Adjust,
    Set,
    Submitting,
}

public sealed record InventoryWorkspaceRow(InventoryStock Stock, Ingredient Ingredient);

public interface IInventoryWorkspaceOperations
{
    Task<Page<InventoryWorkspaceRow>> ListAsync(ListInventoryRequest request, CancellationToken cancellationToken);
    Task<InventoryWorkspaceRow> GetAsync(IngredientId ingredientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(InventoryStock? selected, CancellationToken cancellationToken);
    Task<InventoryStock> AdjustAsync(
        AdjustInventoryRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken);
    Task<InventoryStock> SetAsync(
        SetInventoryRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken);
}

public sealed class InventoryWorkspace : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    private const string TagsField = "Complete tags (optional)";
    private readonly object sync = new();
    private readonly IInventoryWorkspaceOperations operations;
    private readonly WorkspaceRequestTracker requests = new();
    private readonly TableModel<InventoryWorkspaceRow, InventoryId> table = new(
        static row => row.Stock.Id,
        [
            new("Ingredient", static row => row.Ingredient.Name),
            new("Category", static row => row.Ingredient.Category.Value),
            new("Quantity", static row => row.Stock.OnHand.ToString()),
            new("Cost", static row => row.Stock.UnitCost?.ToString() ?? "N/A"),
        ]);
    private readonly List<Cursor> history = [];
    private Dictionary<ActionId, ActionState> actions = [];
    private ListInventoryRequest request = new();
    private CancellationTokenSource? listCancellation;
    private CancellationTokenSource? detailCancellation;
    private InventoryWorkspaceRow? detail;
    private WorkspaceForm? form;
    private Exception? loadError;
    private Exception? actionError;
    private Exception? mutationError;
    private Cursor next;
    private long listGeneration;
    private long detailGeneration;
    private bool loading;
    private bool showFilterHelp;
    private bool disposed;
    private InventoryWorkspaceMode submitOrigin;

    public InventoryWorkspace(IInventoryWorkspaceOperations operations)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public WorkspaceId Id => NavigationProjector.InventoryWorkspace;
    public string Title => "Inventory";
    public InventoryWorkspaceMode Mode { get; private set; }
    public InputOwnership InputOwnership => Mode == InventoryWorkspaceMode.Browse
        ? InputOwnership.Browse
        : InputOwnership.Edit;
    public TuiError? Status
    {
        get
        {
            lock (sync)
            {
                Exception? error = mutationError ?? actionError ?? loadError;
                return error is null ? null : TuiErrorAdapter.Adapt(error);
            }
        }
    }
    public IReadOnlyList<InventoryWorkspaceRow> Rows => table.Rows;
    public InventoryWorkspaceRow? Selected
    {
        get
        {
            lock (sync)
            {
                return table.TryGetSelected(out InventoryWorkspaceRow? selected) ? selected : null;
            }
        }
    }
    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        InventoryModule inventory,
        IngredientsModule ingredients,
        InventoryActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new InventoryWorkspace(
            new ModuleOperations(inventory, ingredients, projector, taggedMutations, session, actor));
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) => RefreshAsync(cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default) => StartListAsync(cancellationToken);

    public Task DrainAsync() => requests.DrainAsync();

    public void SetField(string name, string value)
    {
        lock (sync)
        {
            form?.Set(name, value);
        }

        Changed?.Invoke();
    }

    public bool Handle(char key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (Mode == InventoryWorkspaceMode.Submitting)
        {
            return true;
        }

        if (Mode != InventoryWorkspaceMode.Browse)
        {
            if (key == '\u001b')
            {
                CancelForm();
                return true;
            }

            if (key is SubmitKey or '\r')
            {
                SubmitForm();
                return true;
            }

            lock (sync)
            {
                _ = form?.Handle(key);
            }

            Changed?.Invoke();
            return true;
        }

        switch (key)
        {
            case 'j':
                MoveSelection(1);
                return true;
            case 'k':
                MoveSelection(-1);
                return true;
            case 'f':
                StartFilter();
                return true;
            case 'h':
            case 'H':
                showFilterHelp = !showFilterHelp;
                Changed?.Invoke();
                return true;
            case ']':
                NextPage();
                return true;
            case '[':
                PreviousPage();
                return true;
            case 'a':
                StartAdjust();
                return true;
            case 's':
                StartSet();
                return true;
            case 'r':
                _ = StartListAsync(CancellationToken.None);
                return true;
            default:
                return false;
        }
    }

    public string Render(Viewport viewport)
    {
        lock (sync)
        {
            string content = Mode switch
            {
                InventoryWorkspaceMode.Browse when showFilterHelp => RenderFilterHelp(),
                InventoryWorkspaceMode.Browse => RenderBrowse(viewport),
                _ => form?.Render(FormTitle(), FormFooter()) ?? "Loading form...",
            };
            return WorkspaceRender.Fit(content, viewport);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            _ = ++listGeneration;
            _ = ++detailGeneration;
        }

        await requests.DisposeAsync().ConfigureAwait(false);
    }

    private Task StartListAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = requests.Create(cancellationToken);
        long generation;
        ListInventoryRequest snapshot;
        CancellationTokenSource? previous;
        lock (sync)
        {
            previous = listCancellation;
            listCancellation = source;
            generation = ++listGeneration;
            snapshot = request;
            loading = true;
            loadError = null;
        }

        previous?.Cancel();
        Changed?.Invoke();
        return requests.Track(LoadListAsync(generation, snapshot, source));
    }

    private async Task LoadListAsync(
        long generation,
        ListInventoryRequest snapshot,
        CancellationTokenSource source)
    {
        try
        {
            Page<InventoryWorkspaceRow> page = await operations.ListAsync(snapshot, source.Token).ConfigureAwait(false);
            InventoryId? selected;
            lock (sync)
            {
                if (disposed || generation != listGeneration)
                {
                    return;
                }

                selected = table.TryGetSelected(out InventoryWorkspaceRow? current) ? current?.Stock.Id : null;
                table.Replace(page.Items);
                if (selected is { } id)
                {
                    int index = table.Rows.ToList().FindIndex(row => row.Stock.Id == id);
                    if (index >= 0)
                    {
                        table.Select(index);
                    }
                }

                next = page.Next;
                loading = false;
            }

            StartDetail();
            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync)
            {
                if (!disposed && generation == listGeneration)
                {
                    loading = false;
                }
            }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == listGeneration)
                {
                    loadError = Safe(exception, "load inventory workspace");
                    loading = false;
                }
            }

            Changed?.Invoke();
        }
    }

    private void StartDetail()
    {
        InventoryWorkspaceRow? selected;
        lock (sync)
        {
            selected = table.TryGetSelected(out InventoryWorkspaceRow? value) ? value : null;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        CancellationTokenSource? previous;
        long generation;
        lock (sync)
        {
            previous = detailCancellation;
            detailCancellation = source;
            generation = ++detailGeneration;
            detail = null;
            actionError = null;
        }

        previous?.Cancel();
        _ = requests.Track(LoadDetailAsync(generation, selected, source));
    }

    private async Task LoadDetailAsync(
        long generation,
        InventoryWorkspaceRow? selected,
        CancellationTokenSource source)
    {
        try
        {
            InventoryWorkspaceRow? loaded = selected is null
                ? null
                : await operations.GetAsync(selected.Stock.IngredientId, source.Token).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(loaded?.Stock, source.Token)
                .ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != detailGeneration)
                {
                    return;
                }

                detail = loaded;
                actions = projected.ToDictionary(static state => state.Id);
                actionError = null;
            }

            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == detailGeneration)
                {
                    detail = null;
                    actions = new Dictionary<ActionId, ActionState>();
                    actionError = Safe(exception, "load inventory detail");
                }
            }

            Changed?.Invoke();
        }
    }

    private void MoveSelection(int delta)
    {
        lock (sync)
        {
            if (table.Rows.Count == 0)
            {
                return;
            }

            int nextIndex = Math.Clamp(table.SelectedIndex + delta, 0, table.Rows.Count - 1);
            if (nextIndex == table.SelectedIndex)
            {
                return;
            }

            table.Select(nextIndex);
        }

        StartDetail();
        Changed?.Invoke();
    }

    private void NextPage()
    {
        lock (sync)
        {
            if (next.IsEmpty || !Enabled(InventoryActionProjector.ListAction))
            {
                return;
            }

            history.Add(request.Cursor);
            request = request with { Cursor = next };
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void PreviousPage()
    {
        lock (sync)
        {
            if (history.Count == 0 || !Enabled(InventoryActionProjector.ListAction))
            {
                return;
            }

            int index = history.Count - 1;
            request = request with { Cursor = history[index] };
            history.RemoveAt(index);
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void StartFilter()
    {
        lock (sync)
        {
            if (!Enabled(InventoryActionProjector.ListAction))
            {
                return;
            }

            Mode = InventoryWorkspaceMode.Filter;
            form = new WorkspaceForm(
            [
                new FormField("Stock", request.LowStock.HasValue ? "low stock" : "all", ValidateStockMode),
                new FormField(
                    "Low-stock threshold",
                    (request.LowStock ?? ListInventoryRequest.DefaultLowStockThreshold)
                        .ToString(CultureInfo.InvariantCulture),
                    ValidateNonNegativeNumber),
                new FormField("Expression", request.Filter ?? string.Empty),
                new FormField("Page size", request.EffectiveLimit.ToString(CultureInfo.InvariantCulture), ValidatePositiveInteger),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartAdjust()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(InventoryActionProjector.AdjustAction))
            {
                return;
            }

            Mode = InventoryWorkspaceMode.Adjust;
            form = new WorkspaceForm(
            [
                new FormField("Delta"),
                new FormField("Unit", detail.Stock.OnHand.Unit.Value, ValidateUnit),
                new FormField("Cost per unit"),
                new FormField("Reason", AdjustmentReason.Received.Value, ValidateReason),
                new FormField(TagsField, detail.Stock.Tags.Format()),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartSet()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(InventoryActionProjector.SetAction))
            {
                return;
            }

            Mode = InventoryWorkspaceMode.Set;
            form = new WorkspaceForm(
            [
                new FormField(
                    "Quantity",
                    detail.Stock.OnHand.Value.ToString(CultureInfo.InvariantCulture),
                    ValidateNonNegativeNumber),
                new FormField("Unit", detail.Stock.OnHand.Unit.Value, ValidateUnit),
                new FormField("Cost per unit", detail.Stock.UnitCost?.ToString() ?? string.Empty),
                new FormField(TagsField, detail.Stock.Tags.Format()),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void SubmitForm()
    {
        WorkspaceForm active;
        InventoryWorkspaceMode origin;
        lock (sync)
        {
            if (form is null || !form.Model.TryBeginSubmit())
            {
                Changed?.Invoke();
                return;
            }

            active = form;
            origin = Mode;
        }

        if (origin == InventoryWorkspaceMode.Filter)
        {
            try
            {
                ApplyFilter(active);
            }
            catch (Exception exception)
            {
                active.Model.FailSubmit(TuiErrorAdapter.Adapt(exception).Message);
                Changed?.Invoke();
            }

            return;
        }

        Func<CancellationToken, Task<InventoryStock>> mutation;
        try
        {
            mutation = BuildMutation(origin, active);
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(exception).Message);
            Changed?.Invoke();
            return;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        lock (sync)
        {
            submitOrigin = origin;
            Mode = InventoryWorkspaceMode.Submitting;
            mutationError = null;
        }

        Changed?.Invoke();
        _ = requests.Track(RunMutationAsync(mutation, active, source));
    }

    private void ApplyFilter(WorkspaceForm active)
    {
        double threshold = double.Parse(active["Low-stock threshold"], CultureInfo.InvariantCulture);
        double? lowStock = string.Equals(active["Stock"].Trim(), "low stock", StringComparison.Ordinal)
            ? threshold
            : null;
        int limit = int.Parse(active["Page size"], CultureInfo.InvariantCulture);
        active.Model.CompleteSubmit();
        lock (sync)
        {
            request = new ListInventoryRequest(
                LowStock: lowStock,
                Filter: active["Expression"].Trim(),
                Limit: limit);
            history.Clear();
            next = default;
            Mode = InventoryWorkspaceMode.Browse;
            form = null;
            showFilterHelp = false;
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private Func<CancellationToken, Task<InventoryStock>> BuildMutation(
        InventoryWorkspaceMode origin,
        WorkspaceForm active)
    {
        InventoryWorkspaceRow target = detail ?? throw AppError.FailedPrecondition("inventory form has no target");
        TagCollection? tags = active.DesiredTags(TagsField);
        return origin switch
        {
            InventoryWorkspaceMode.Adjust => token => operations.AdjustAsync(
                ParseAdjustment(target, active), tags, token),
            InventoryWorkspaceMode.Set => token => operations.SetAsync(
                ParseSet(target, active), tags, token),
            _ => throw AppError.FailedPrecondition("inventory form has no mutation"),
        };
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<InventoryStock>> mutation,
        WorkspaceForm active,
        CancellationTokenSource source)
    {
        try
        {
            _ = await mutation(source.Token).ConfigureAwait(false);
            active.Model.CompleteSubmit();
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                Mode = InventoryWorkspaceMode.Browse;
                form = null;
                mutationError = null;
            }

            _ = StartListAsync(CancellationToken.None);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync)
            {
                if (!disposed)
                {
                    Mode = submitOrigin;
                    active.Model.FailSubmit("operation cancelled");
                }
            }
        }
        catch (Exception exception)
        {
            Exception safe = Safe(exception, "mutate inventory from TUI");
            lock (sync)
            {
                if (!disposed)
                {
                    Mode = submitOrigin;
                    mutationError = safe;
                    active.Model.FailSubmit(TuiErrorAdapter.Adapt(safe).Message);
                }
            }

            Changed?.Invoke();
        }
    }

    private void CancelForm()
    {
        lock (sync)
        {
            if (form?.Model.Mode == FormMode.Edit)
            {
                form.Model.CancelEdit();
            }

            form = null;
            Mode = InventoryWorkspaceMode.Browse;
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private bool Enabled(ActionId id) =>
        actions.TryGetValue(id, out ActionState? state) && state.Visible && state.Enabled;

    private string RenderBrowse(Viewport viewport)
    {
        double threshold = request.LowStock ?? ListInventoryRequest.DefaultLowStockThreshold;
        List<string> list =
        [
            $"Inventory · page {history.Count + 1} · size {request.EffectiveLimit}",
            loading ? "Loading..." : $"{table.Rows.Count} result(s)",
            string.Empty,
        ];
        int rowLimit = Math.Max(viewport.Height - 8, 1);
        foreach ((InventoryWorkspaceRow row, int index) in table.Rows.Take(rowLimit).Select((value, index) => (value, index)))
        {
            string marker = index == table.SelectedIndex ? ">" : " ";
            list.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{marker} {row.Ingredient.Name} · {row.Stock.OnHand} · {StockStatus(row.Stock, threshold)}"));
        }

        List<string> selected = detail is null
            ? ["Select a stock item to view details"]
            : DetailLines(detail, threshold);
        string body = WorkspaceRender.TwoPane(list, selected, viewport.Width);
        return string.Join('\n', body, string.Empty, BrowseHelp());
    }

    private string BrowseHelp()
    {
        List<string> keys = ["[j/k] select", "[f] filter", "[h] filter help", "[[/]] page", "[r] refresh"];
        if (Enabled(InventoryActionProjector.AdjustAction))
        {
            keys.Add("[a] adjust");
        }

        if (Enabled(InventoryActionProjector.SetAction))
        {
            keys.Add("[s] set");
        }

        return string.Join("  ", keys);
    }

    private static List<string> DetailLines(InventoryWorkspaceRow row, double threshold) =>
    [
        row.Ingredient.Name,
        $"Ingredient ID: {row.Ingredient.Id}",
        $"Inventory ID: {row.Stock.Id}",
        $"Tags: {(row.Stock.Tags.Count == 0 ? "(none)" : row.Stock.Tags.Format())}",
        $"Category: {row.Ingredient.Category}",
        $"Unit: {row.Ingredient.Unit}",
        string.Empty,
        $"Quantity: {row.Stock.OnHand}",
        $"Reserved: {row.Stock.Reserved}",
        $"Available: {row.Stock.Available}",
        $"Cost per unit: {row.Stock.UnitCost?.ToString() ?? "N/A"}",
        $"Status: {StockStatus(row.Stock, threshold)}",
        $"Last updated: {row.Stock.LastUpdated:O}",
    ];

    private static string RenderFilterHelp() => """
        Inventory filter help · [h] close

        Fields: id, ingredient_id, quantity, unit, last_updated, tags
        Comparisons: == != < <= > >= in not in
        Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches

        quantity <= 5 && unit == "ml"
        ingredient_id.startsWith("ing-") || quantity == 0
        tags contains "featured" || tags contains "region=west"
        """;

    private string FormTitle() => Mode switch
    {
        InventoryWorkspaceMode.Filter => "Filter Inventory",
        InventoryWorkspaceMode.Adjust => $"Adjust Inventory: {detail?.Ingredient.Name}",
        InventoryWorkspaceMode.Set => $"Set Inventory: {detail?.Ingredient.Name}",
        InventoryWorkspaceMode.Submitting => "Submitting inventory mutation...",
        _ => "Inventory",
    };

    private string FormFooter() => Mode == InventoryWorkspaceMode.Submitting
        ? "Submitting..."
        : "[Tab] next field · [Ctrl+S] submit · [Esc] cancel";

    private static AdjustInventoryRequest ParseAdjustment(
        InventoryWorkspaceRow target,
        WorkspaceForm active)
    {
        string rawDelta = active["Delta"].Trim();
        string rawCost = active["Cost per unit"].Trim();
        Amount? delta = rawDelta.Length == 0
            ? null
            : Amount.Create(ParseDouble(rawDelta, "delta"), Unit.Parse(active["Unit"]));
        Price? cost = rawCost.Length == 0 ? null : Price.Parse(rawCost);
        return new AdjustInventoryRequest(
            target.Stock.IngredientId,
            AdjustmentReason.Parse(active["Reason"]),
            delta,
            cost).Normalize();
    }

    private static SetInventoryRequest ParseSet(InventoryWorkspaceRow target, WorkspaceForm active)
    {
        Amount quantity = Amount.Create(
            ParseDouble(active["Quantity"], "quantity"),
            Unit.Parse(active["Unit"]));
        string rawCost = active["Cost per unit"].Trim();
        Price cost = rawCost.Length == 0
            ? target.Stock.UnitCost ?? new Price(0m, Currency.Usd)
            : Price.Parse(rawCost);
        return new SetInventoryRequest(target.Stock.IngredientId, quantity, cost, target.Stock.Revision).Normalize();
    }

    private static double ParseDouble(string value, string name) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : throw AppError.Invalid($"{name} must be a number");

    private static string StockStatus(InventoryStock stock, double threshold)
    {
        double available = stock.Available.Value;
        return available <= 0d ? "OUT" : available <= threshold ? "LOW" : "OK";
    }

    private static string? ValidateStockMode(string value) => value.Trim() is "all" or "low stock"
        ? null
        : "stock must be all or low stock";

    private static string? ValidateNonNegativeNumber(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
        double.IsFinite(parsed) && parsed >= 0d
            ? null
            : "value must be a finite number greater than or equal to zero";

    private static string? ValidatePositiveInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? null
            : "page size must be greater than zero";

    private static string? ValidateUnit(string value)
    {
        try
        {
            _ = Unit.Parse(value);
            return null;
        }
        catch (InvalidError error)
        {
            return error.UserMessage;
        }
    }

    private static string? ValidateReason(string value)
    {
        try
        {
            _ = AdjustmentReason.Parse(value);
            return null;
        }
        catch (InvalidError error)
        {
            return error.UserMessage;
        }
    }

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        InventoryModule inventory,
        IngredientsModule ingredients,
        InventoryActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor) : IInventoryWorkspaceOperations
    {
        public async Task<Page<InventoryWorkspaceRow>> ListAsync(
            ListInventoryRequest request,
            CancellationToken cancellationToken)
        {
            Page<InventoryStock> page = await inventory.ListAsync(session, request, cancellationToken)
                .ConfigureAwait(false);
            Dictionary<IngredientId, Ingredient> ingredientById = [];
            foreach (IngredientId id in page.Items.Select(static stock => stock.IngredientId).Distinct())
            {
                try
                {
                    ingredientById[id] = await ingredients.GetAsync(session, id, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    !AppError.IsCancellation(exception) && AppError.IsNotFound(exception))
                {
                    throw AppError.Internal($"inventory ingredient {id} is missing", exception);
                }
            }

            return new Page<InventoryWorkspaceRow>(
                page.Items.Select(stock => new InventoryWorkspaceRow(stock, ingredientById[stock.IngredientId])).ToArray(),
                page.Next);
        }

        public async Task<InventoryWorkspaceRow> GetAsync(
            IngredientId ingredientId,
            CancellationToken cancellationToken)
        {
            InventoryStock stock = await inventory.GetAsync(session, ingredientId, cancellationToken)
                .ConfigureAwait(false);
            Ingredient ingredient;
            try
            {
                ingredient = await ingredients.GetAsync(session, ingredientId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                !AppError.IsCancellation(exception) && AppError.IsNotFound(exception))
            {
                throw AppError.Internal($"inventory ingredient {ingredientId} is missing", exception);
            }

            return new InventoryWorkspaceRow(stock, ingredient);
        }

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            InventoryStock? selected,
            CancellationToken cancellationToken) =>
            projector.ProjectAsync(actor, selected, cancellationToken);

        public Task<InventoryStock> AdjustAsync(
            AdjustInventoryRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                (active, token) => inventory.AdjustAsync(active, request, token),
                desiredTags,
                static stock => stock.EntityUid,
                static (stock, tags) => stock with { Tags = tags },
                cancellationToken);

        public Task<InventoryStock> SetAsync(
            SetInventoryRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                (active, token) => inventory.SetAsync(active, request, token),
                desiredTags,
                static stock => stock.EntityUid,
                static (stock, tags) => stock with { Tags = tags },
                cancellationToken);
    }
}
