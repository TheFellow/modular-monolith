using System.Globalization;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Orders.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;

namespace Mixology.Tui.Workspaces.Orders;

public enum OrdersWorkspaceMode
{
    Browse,
    Filter,
    Place,
    Complete,
    Cancel,
    Submitting,
}

public sealed record OrderDrinkOption(DrinkId Id, string Name);
public sealed record OrderMenuOption(MenuId Id, string Name, IReadOnlyList<OrderDrinkOption> Drinks);
public sealed record OrderPlacementLine(OrderDrinkOption Drink, int Quantity, string Notes);

public interface IOrdersWorkspaceOperations
{
    Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken);
    Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderMenuOption>> CatalogAsync(CancellationToken cancellationToken);
    Task<Order> PlaceAsync(PlaceOrderRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Order> CompleteAsync(OrderId id, TagCollection? tags, CancellationToken cancellationToken);
    Task<Order> CancelAsync(OrderId id, TagCollection? tags, CancellationToken cancellationToken);
}

public sealed class OrdersWorkspace : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    private const string TagsField = "Complete tags (optional)";
    private readonly object sync = new();
    private readonly IOrdersWorkspaceOperations operations;
    private readonly WorkspaceRequestTracker requests = new();
    private readonly TableModel<Order, OrderId> table = new(
        static order => order.Id,
        [
            new("ID", static order => order.Id.Value),
            new("Status", static order => order.Status.Value),
            new("Items", static order => order.Items.Count.ToString(CultureInfo.InvariantCulture)),
        ]);
    private readonly List<Cursor> history = [];
    private Dictionary<ActionId, ActionState> actions = [];
    private ListOrdersRequest request = new();
    private CancellationTokenSource? listCancellation;
    private CancellationTokenSource? detailCancellation;
    private CancellationTokenSource? workflowCancellation;
    private Order? detail;
    private WorkspaceForm? form;
    private OrderPlacementEditor? placement;
    private Exception? loadError;
    private Exception? actionError;
    private Exception? mutationError;
    private Cursor next;
    private long listGeneration;
    private long detailGeneration;
    private long workflowGeneration;
    private OrderId? restoreId;
    private bool loading;
    private bool showFilterHelp;
    private bool disposed;
    private OrdersWorkspaceMode submitOrigin;

    public OrdersWorkspace(IOrdersWorkspaceOperations operations)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public WorkspaceId Id => NavigationProjector.OrdersWorkspace;
    public string Title => "Orders";
    public OrdersWorkspaceMode Mode { get; private set; }
    public InputOwnership InputOwnership => Mode == OrdersWorkspaceMode.Browse
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

    public IReadOnlyList<Order> Rows => table.Rows;
    public Order? Selected
    {
        get { lock (sync) { return table.TryGetSelected(out Order? value) ? value : null; } }
    }

    public OrderPlacementEditor? Placement { get { lock (sync) { return placement; } } }
    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        OrdersModule orders,
        MenusModule menus,
        DrinksModule drinks,
        OrderActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(drinks);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new OrdersWorkspace(
            new ModuleOperations(orders, menus, drinks, projector, taggedMutations, session, actor));
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) => StartListAsync(cancellationToken);
    public Task RefreshAsync(CancellationToken cancellationToken = default) => StartListAsync(cancellationToken);
    public Task DrainAsync() => requests.DrainAsync();

    public void SetField(string name, string value)
    {
        lock (sync) { form?.Set(name, value); }
        Changed?.Invoke();
    }

    public bool Handle(char key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Mode == OrdersWorkspaceMode.Submitting) { return true; }
        if (Mode == OrdersWorkspaceMode.Place) { return HandlePlacement(key); }
        if (Mode is OrdersWorkspaceMode.Complete or OrdersWorkspaceMode.Cancel)
        {
            return HandleConfirmation(key);
        }

        if (Mode == OrdersWorkspaceMode.Filter)
        {
            if (key == '\u001b') { CancelWorkflow(); return true; }
            if (key is SubmitKey or '\r') { SubmitFilter(); return true; }
            lock (sync) { _ = form?.Handle(key); }
            Changed?.Invoke();
            return true;
        }

        switch (key)
        {
            case 'j': MoveSelection(1); return true;
            case 'k': MoveSelection(-1); return true;
            case 'f': StartFilter(); return true;
            case 'h':
            case 'H': showFilterHelp = !showFilterHelp; Changed?.Invoke(); return true;
            case ']': NextPage(); return true;
            case '[': PreviousPage(); return true;
            case 'c': StartPlacement(); return true;
            case 'o': StartConfirmation(OrdersWorkspaceMode.Complete, OrderActionProjector.CompleteAction); return true;
            case 'x': StartConfirmation(OrdersWorkspaceMode.Cancel, OrderActionProjector.CancelAction); return true;
            case 'r': _ = StartListAsync(CancellationToken.None); return true;
            default: return false;
        }
    }

    public string Render(Viewport viewport)
    {
        lock (sync)
        {
            string content = Mode switch
            {
                OrdersWorkspaceMode.Browse when showFilterHelp => RenderFilterHelp(),
                OrdersWorkspaceMode.Browse => RenderBrowse(viewport),
                OrdersWorkspaceMode.Filter => form?.Render(
                    "Filter Orders", "[Tab] next field · [Ctrl+S] apply · [Esc] cancel") ?? "Loading filter...",
                OrdersWorkspaceMode.Place => placement?.Render() ?? "Loading published menus...",
                OrdersWorkspaceMode.Complete => Confirmation("Complete"),
                OrdersWorkspaceMode.Cancel => Confirmation("Cancel"),
                _ => "Submitting order mutation...",
            };
            return WorkspaceRender.Fit(content, viewport);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            _ = ++listGeneration;
            _ = ++detailGeneration;
            _ = ++workflowGeneration;
        }

        await requests.DisposeAsync().ConfigureAwait(false);
    }

    private Task StartListAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = requests.Create(cancellationToken);
        CancellationTokenSource? previous;
        long generation;
        ListOrdersRequest snapshot;
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

    private async Task LoadListAsync(long generation, ListOrdersRequest snapshot, CancellationTokenSource source)
    {
        try
        {
            Page<Order> page = await operations.ListAsync(snapshot, source.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != listGeneration) { return; }
                OrderId? selected = restoreId
                    ?? (table.TryGetSelected(out Order? current) ? current?.Id : null);
                table.Replace(page.Items);
                if (selected is { } id)
                {
                    int index = table.Rows.ToList().FindIndex(item => item.Id == id);
                    if (index >= 0) { table.Select(index); }
                }

                next = page.Next;
                restoreId = null;
                loading = false;
            }

            StartDetail();
            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync) { if (!disposed && generation == listGeneration) { loading = false; } }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == listGeneration)
                {
                    loadError = Safe(exception, "load orders workspace");
                    loading = false;
                }
            }

            Changed?.Invoke();
        }
    }

    private void StartDetail()
    {
        Order? selected;
        lock (sync) { selected = table.TryGetSelected(out Order? value) ? value : null; }
        CancellationTokenSource source = requests.Create(CancellationToken.None);
        CancellationTokenSource? previous;
        long generation;
        lock (sync)
        {
            previous = detailCancellation;
            detailCancellation = source;
            generation = ++detailGeneration;
            detail = null;
            actions = [];
            actionError = null;
        }

        previous?.Cancel();
        _ = requests.Track(LoadDetailAsync(generation, selected, source));
    }

    private async Task LoadDetailAsync(long generation, Order? selected, CancellationTokenSource source)
    {
        try
        {
            Order? loaded = selected is null
                ? null
                : await operations.GetAsync(selected.Id, source.Token).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(loaded, source.Token)
                .ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != detailGeneration) { return; }
                detail = loaded;
                actions = projected.ToDictionary(static state => state.Id);
                actionError = null;
            }

            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == detailGeneration)
                {
                    detail = null;
                    actions = [];
                    actionError = Safe(exception, "load order detail");
                }
            }

            Changed?.Invoke();
        }
    }

    private void MoveSelection(int delta)
    {
        lock (sync)
        {
            if (table.Rows.Count == 0) { return; }
            int index = Math.Clamp(table.SelectedIndex + delta, 0, table.Rows.Count - 1);
            if (index == table.SelectedIndex) { return; }
            table.Select(index);
        }

        StartDetail();
        Changed?.Invoke();
    }

    private void NextPage()
    {
        lock (sync)
        {
            if (next.IsEmpty || !Enabled(OrderActionProjector.ListAction)) { return; }
            history.Add(request.Cursor);
            request = request with { Cursor = next };
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void PreviousPage()
    {
        lock (sync)
        {
            if (history.Count == 0 || !Enabled(OrderActionProjector.ListAction)) { return; }
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
            if (!Enabled(OrderActionProjector.ListAction)) { return; }
            Mode = OrdersWorkspaceMode.Filter;
            form = new WorkspaceForm(
            [
                new FormField("Status", request.Status?.Value ?? string.Empty, ValidateOptionalStatus),
                new FormField("Menu ID", request.MenuId?.Value ?? string.Empty, ValidateOptionalMenuId),
                new FormField("Expression", request.Filter ?? string.Empty),
                new FormField("Page size", request.EffectiveLimit.ToString(CultureInfo.InvariantCulture), ValidatePositiveInteger),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void SubmitFilter()
    {
        WorkspaceForm active;
        lock (sync)
        {
            if (form is null || !form.Model.TryBeginSubmit()) { return; }
            active = form;
        }

        try
        {
            OrderStatus? status = string.IsNullOrWhiteSpace(active["Status"])
                ? null
                : OrderStatus.Parse(active["Status"]);
            MenuId? menuId = string.IsNullOrWhiteSpace(active["Menu ID"])
                ? null
                : MenuId.Parse(active["Menu ID"]);
            int limit = int.Parse(active["Page size"], CultureInfo.InvariantCulture);
            active.Model.CompleteSubmit();
            lock (sync)
            {
                request = new ListOrdersRequest(status, menuId, active["Expression"], default, limit).Normalize();
                history.Clear();
                next = default;
                Mode = OrdersWorkspaceMode.Browse;
                form = null;
                showFilterHelp = false;
            }

            _ = StartListAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "apply order filter")).Message);
            Changed?.Invoke();
        }
    }

    private void StartPlacement()
    {
        lock (sync)
        {
            if (!Enabled(OrderActionProjector.PlaceAction)) { return; }
            Mode = OrdersWorkspaceMode.Place;
            placement = new OrderPlacementEditor();
            mutationError = null;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        CancellationTokenSource? previous;
        long generation;
        lock (sync)
        {
            previous = workflowCancellation;
            workflowCancellation = source;
            generation = ++workflowGeneration;
        }

        previous?.Cancel();
        Changed?.Invoke();
        _ = requests.Track(LoadCatalogAsync(generation, source));
    }

    private async Task LoadCatalogAsync(long generation, CancellationTokenSource source)
    {
        try
        {
            IReadOnlyList<OrderMenuOption> catalog = await operations.CatalogAsync(source.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != workflowGeneration || placement is null) { return; }
                placement.SetCatalog(catalog);
            }

            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == workflowGeneration && placement is not null)
                {
                    mutationError = Safe(exception, "load order placement catalog");
                    placement.SetError(TuiErrorAdapter.Adapt(mutationError).Message);
                }
            }

            Changed?.Invoke();
        }
    }

    private bool HandlePlacement(char key)
    {
        if (key == '\u001b')
        {
            CancelWorkflow();
            return true;
        }

        if (key == SubmitKey)
        {
            SubmitPlacement();
            return true;
        }

        lock (sync) { placement?.Handle(key); }
        Changed?.Invoke();
        return true;
    }

    private void SubmitPlacement()
    {
        OrderPlacementEditor editor;
        PlaceOrderRequest requestValue;
        TagCollection? tags;
        lock (sync)
        {
            if (placement is null || placement.Saving) { return; }
            editor = placement;
            try
            {
                requestValue = editor.Build();
                tags = editor.DesiredTags();
            }
            catch (Exception exception)
            {
                mutationError = Safe(exception, "build order placement");
                editor.SetError(TuiErrorAdapter.Adapt(mutationError).Message);
                Changed?.Invoke();
                return;
            }

            editor.Saving = true;
            submitOrigin = OrdersWorkspaceMode.Place;
            Mode = OrdersWorkspaceMode.Submitting;
            mutationError = null;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        _ = requests.Track(RunMutationAsync(
            token => operations.PlaceAsync(requestValue, tags, token), editor, source));
        Changed?.Invoke();
    }

    private void StartConfirmation(OrdersWorkspaceMode mode, ActionId action)
    {
        lock (sync)
        {
            if (detail is null || !Enabled(action)) { return; }
            Mode = mode;
            form = new WorkspaceForm([new FormField(TagsField, detail.Tags.Format())]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private bool HandleConfirmation(char key)
    {
        if (key == '\u001b') { CancelWorkflow(); return true; }
        if (key is SubmitKey or '\r') { SubmitConfirmation(); return true; }
        lock (sync) { _ = form?.Handle(key); }
        Changed?.Invoke();
        return true;
    }

    private void SubmitConfirmation()
    {
        Order target;
        WorkspaceForm active;
        OrdersWorkspaceMode origin;
        TagCollection? tags;
        lock (sync)
        {
            if (detail is null || form is null || !form.Model.TryBeginSubmit()) { return; }
            target = detail;
            active = form;
            origin = Mode;
            try { tags = active.DesiredTags(TagsField); }
            catch (Exception exception)
            {
                active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "parse order tags")).Message);
                Changed?.Invoke();
                return;
            }

            submitOrigin = origin;
            Mode = OrdersWorkspaceMode.Submitting;
        }

        Func<CancellationToken, Task<Order>> mutation = origin == OrdersWorkspaceMode.Complete
            ? token => operations.CompleteAsync(target.Id, tags, token)
            : token => operations.CancelAsync(target.Id, tags, token);
        CancellationTokenSource source = requests.Create(CancellationToken.None);
        _ = requests.Track(RunMutationAsync(mutation, editor: null, source, active));
        Changed?.Invoke();
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<Order>> mutation,
        OrderPlacementEditor? editor,
        CancellationTokenSource source,
        WorkspaceForm? active = null)
    {
        try
        {
            Order mutated = await mutation(source.Token).ConfigureAwait(false);
            active?.Model.CompleteSubmit();
            lock (sync)
            {
                if (disposed) { return; }
                Mode = OrdersWorkspaceMode.Browse;
                placement = null;
                form = null;
                mutationError = null;
                restoreId = mutated.Id;
                request = request with { Cursor = default };
                history.Clear();
                next = default;
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
                    if (editor is not null) { editor.Saving = false; editor.SetError("operation cancelled"); }
                    active?.Model.FailSubmit("operation cancelled");
                }
            }

            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Exception safe = Safe(exception, "mutate order from TUI");
            lock (sync)
            {
                if (!disposed)
                {
                    Mode = submitOrigin;
                    mutationError = safe;
                    if (editor is not null)
                    {
                        editor.Saving = false;
                        editor.SetError(TuiErrorAdapter.Adapt(safe).Message);
                    }

                    active?.Model.FailSubmit(TuiErrorAdapter.Adapt(safe).Message);
                }
            }

            Changed?.Invoke();
        }
    }

    private void CancelWorkflow()
    {
        lock (sync)
        {
            workflowCancellation?.Cancel();
            _ = ++workflowGeneration;
            Mode = OrdersWorkspaceMode.Browse;
            placement = null;
            form = null;
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private bool Enabled(ActionId id) =>
        actions.TryGetValue(id, out ActionState? state) && state.Visible && state.Enabled;

    private string RenderBrowse(Viewport viewport)
    {
        List<string> list =
        [
            $"Orders · page {history.Count + 1} · size {request.EffectiveLimit}",
            loading ? "Loading..." : $"{table.Rows.Count} result(s)",
            string.Empty,
        ];
        int rowLimit = Math.Max(viewport.Height - 8, 1);
        foreach ((Order order, int index) in table.Rows.Take(rowLimit).Select((value, index) => (value, index)))
        {
            string marker = index == table.SelectedIndex ? ">" : " ";
            list.Add($"{marker} {order.Id} · {order.Status} · {order.Items.Count} item(s)");
        }

        List<string> selected = detail is null ? ["Select an order to view details"] : DetailLines();
        List<string> footer = WrapHelp(BrowseHelp(), viewport.Width);
        string body = string.Join('\n', WorkspaceRender.TwoPane(list, selected, viewport.Width)
            .Split('\n').Take(Math.Max(viewport.Height - footer.Count - 1, 1)));
        return string.Join('\n', [body, string.Empty, .. footer]);
    }

    private List<string> DetailLines()
    {
        Order order = detail!;
        List<string> lines =
        [
            "Order",
            $"ID: {order.Id}",
            $"Menu ID: {order.MenuId}",
            $"Status: {order.Status}",
            $"Tags: {(order.Tags.Count == 0 ? "(none)" : order.Tags.Format())}",
            $"Created: {order.CreatedAt:O}",
        ];
        if (order.CompletedAt is { } completed) { lines.Add($"Completed: {completed:O}"); }
        if (order.BlockedIngredientIds.Count > 0)
        {
            lines.Add($"Short of reserved stock: {string.Join(", ", order.BlockedIngredientIds)}");
        }

        AddDisabledReason(lines, OrderActionProjector.CompleteAction, "Complete");
        AddDisabledReason(lines, OrderActionProjector.CancelAction, "Cancel order");
        if (!string.IsNullOrWhiteSpace(order.Notes)) { lines.Add($"Notes: {order.Notes}"); }
        lines.Add("Items:");
        foreach (OrderItem item in order.Items)
        {
            lines.Add($"- {item.DrinkId} × {item.Quantity}{(item.Notes.Length == 0 ? string.Empty : $" · {item.Notes}")}");
        }

        lines.Add("Ingredient usage snapshot:");
        foreach (IngredientUsage usage in order.IngredientUsage)
        {
            lines.Add($"- {usage.Name}: {usage.Amount}");
        }

        return lines;
    }

    private void AddDisabledReason(List<string> lines, ActionId id, string label)
    {
        if (actions.TryGetValue(id, out ActionState? state)
            && state.Visible && !state.Enabled && state.DisabledReason.Length > 0)
        {
            lines.Add($"{label}: {state.DisabledReason}");
        }
    }

    private string BrowseHelp()
    {
        List<string> keys = ["[j/k] select", "[f] filter", "[h] help", "[[/]] page", "[r] refresh"];
        if (Enabled(OrderActionProjector.PlaceAction)) { keys.Add("[c] place"); }
        if (Enabled(OrderActionProjector.CompleteAction)) { keys.Add("[o] complete"); }
        if (Enabled(OrderActionProjector.CancelAction)) { keys.Add("[x] cancel"); }
        return string.Join("  ", keys);
    }

    private string Confirmation(string verb) =>
        $"{verb} order {detail?.Id}?\n\n[Enter/Ctrl+S] confirm · [Esc] cancel";

    private static List<string> WrapHelp(string help, int width)
    {
        List<string> lines = [];
        foreach (string part in help.Split("  ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (lines.Count == 0 || lines[^1].Length + part.Length + 2 > width)
            {
                lines.Add(part);
            }
            else
            {
                lines[^1] += "  " + part;
            }
        }

        return lines;
    }

    private static string RenderFilterHelp() => """
        Order filter help · [h] close

        Fields: id, menuId, status, createdAt, notes, tags
        Comparisons: == != < <= > >= in not in
        Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches

        status == "pending" && notes.contains("patio")
        tags contains "vip" || menuId == "menu-..."
        """;

    private static string? ValidateOptionalStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        try { _ = OrderStatus.Parse(value); return null; }
        catch (InvalidError error) { return error.UserMessage; }
    }

    private static string? ValidateOptionalMenuId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        try { _ = MenuId.Parse(value); return null; }
        catch (InvalidError error) { return error.UserMessage; }
    }

    private static string? ValidatePositiveInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? null
            : "page size must be greater than zero";

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        OrdersModule orders,
        MenusModule menus,
        DrinksModule drinks,
        OrderActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor) : IOrdersWorkspaceOperations
    {
        public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken) =>
            orders.ListAsync(session, request, cancellationToken);

        public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken) =>
            orders.GetAsync(session, id, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectAsync(Order? selected, CancellationToken cancellationToken) =>
            projector.ProjectAsync(actor, selected, cancellationToken);

        public async Task<IReadOnlyList<OrderMenuOption>> CatalogAsync(CancellationToken cancellationToken)
        {
            List<OrderMenuOption> result = [];
            Cursor cursor = default;
            do
            {
                Page<Menu> page = await menus.ListAsync(
                    session,
                    new ListMenusRequest(MenuStatus.Published, Cursor: cursor),
                    cancellationToken).ConfigureAwait(false);
                foreach (Menu menu in page.Items)
                {
                    List<OrderDrinkOption> menuDrinks = [];
                    foreach (MenuItem item in menu.Items.Where(static item => item.Availability != Availability.Unavailable))
                    {
                        string name = item.DisplayName ?? (await drinks.GetAsync(
                            session,
                            item.DrinkId,
                            cancellationToken).ConfigureAwait(false)).Name;
                        menuDrinks.Add(new OrderDrinkOption(item.DrinkId, name));
                    }

                    result.Add(new OrderMenuOption(
                        menu.Id,
                        menu.Name,
                        menuDrinks.OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(static option => option.Id.Value, StringComparer.Ordinal).ToArray()));
                }

                cursor = page.Next;
            }
            while (!cursor.IsEmpty);
            return result.OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static option => option.Id.Value, StringComparer.Ordinal).ToArray();
        }

        public Task<Order> PlaceAsync(
            PlaceOrderRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => orders.PlaceAsync(active, request, token), tags, cancellationToken);

        public Task<Order> CompleteAsync(
            OrderId id,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => orders.CompleteAsync(active, id, token), tags, cancellationToken);

        public Task<Order> CancelAsync(
            OrderId id,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => orders.CancelAsync(active, id, token), tags, cancellationToken);

        private Task<Order> Tagged(
            Func<MixologySession, CancellationToken, Task<Order>> mutate,
            TagCollection? tags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                mutate,
                tags,
                static value => value.EntityUid,
                static (value, desired) => value with { Tags = desired },
                cancellationToken);
    }
}

public enum OrderPlacementField
{
    Menu,
    Drink,
    Quantity,
    ItemNotes,
    OrderNotes,
    Tags,
}

public sealed class OrderPlacementEditor
{
    private readonly List<OrderPlacementLine> lines = [];
    private IReadOnlyList<OrderMenuOption> menus = [];
    private List<OrderMenuOption> visibleMenus = [];
    private List<OrderDrinkOption> visibleDrinks = [];
    private OrderMenuOption? menu;
    private string menuQuery = string.Empty;
    private string drinkQuery = string.Empty;
    private string quantity = "1";
    private string itemNotes = string.Empty;
    private string orderNotes = string.Empty;
    private string tags = string.Empty;
    private int menuIndex;
    private int drinkIndex;
    private bool loaded;

    public OrderPlacementField Field { get; private set; }
    public IReadOnlyList<OrderPlacementLine> Lines => lines;
    public OrderMenuOption? Menu => menu;
    public bool Saving { get; set; }
    public string? Error { get; private set; }

    public void SetCatalog(IReadOnlyList<OrderMenuOption> values)
    {
        menus = values;
        loaded = true;
        FilterMenus();
    }

    public void SetError(string error) => Error = error;

    public void SetField(OrderPlacementField field, string value)
    {
        switch (field)
        {
            case OrderPlacementField.Menu: menuQuery = value; FilterMenus(); break;
            case OrderPlacementField.Drink: drinkQuery = value; FilterDrinks(); break;
            case OrderPlacementField.Quantity: quantity = value; break;
            case OrderPlacementField.ItemNotes: itemNotes = value; break;
            case OrderPlacementField.OrderNotes: orderNotes = value; break;
            case OrderPlacementField.Tags: tags = value; break;
        }
    }

    public void Handle(char key)
    {
        if (Saving) { return; }
        Error = null;
        if (key == '\t')
        {
            Field = (OrderPlacementField)(((int)Field + 1) % Enum.GetValues<OrderPlacementField>().Length);
            return;
        }

        if (key == '\r')
        {
            if (Field == OrderPlacementField.Menu) { ChooseMenu(); }
            else if (Field == OrderPlacementField.Drink) { AddSelectedDrink(); }
            return;
        }

        if (key is 'j' or 'k' && Field is OrderPlacementField.Menu or OrderPlacementField.Drink)
        {
            int delta = key == 'j' ? 1 : -1;
            if (Field == OrderPlacementField.Menu)
            {
                menuIndex = Math.Clamp(menuIndex + delta, 0, Math.Max(visibleMenus.Count - 1, 0));
            }
            else
            {
                drinkIndex = Math.Clamp(drinkIndex + delta, 0, Math.Max(visibleDrinks.Count - 1, 0));
            }

            return;
        }

        if (key is '\b' or '\u007f')
        {
            SetField(Field, Chop(Value(Field)));
            return;
        }

        if (key == '\n' && Field is OrderPlacementField.ItemNotes or OrderPlacementField.OrderNotes)
        {
            SetField(Field, Value(Field) + "\n");
            return;
        }

        if (!char.IsControl(key)) { SetField(Field, Value(Field) + key); }
    }

    public void ChooseMenu()
    {
        if (visibleMenus.Count == 0) { Error = "select a published menu"; return; }
        menu = visibleMenus[menuIndex];
        drinkQuery = string.Empty;
        FilterDrinks();
        Field = OrderPlacementField.Drink;
    }

    public void AddSelectedDrink()
    {
        if (menu is null) { Error = "select a published menu"; return; }
        if (visibleDrinks.Count == 0) { Error = "select an available drink"; return; }
        if (!int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
        {
            Error = "quantity must be greater than zero";
            return;
        }

        OrderDrinkOption drink = visibleDrinks[drinkIndex];
        int existing = lines.FindIndex(line => line.Drink.Id == drink.Id);
        if (existing >= 0)
        {
            OrderPlacementLine current = lines[existing];
            try
            {
                lines[existing] = current with
                {
                    Quantity = checked(current.Quantity + parsed),
                    Notes = itemNotes.Trim(),
                };
            }
            catch (OverflowException exception)
            {
                Error = AppError.Invalid("quantity is too large", exception).UserMessage;
                return;
            }
        }
        else
        {
            lines.Add(new OrderPlacementLine(drink, parsed, itemNotes.Trim()));
        }

        itemNotes = string.Empty;
        quantity = "1";
    }

    public PlaceOrderRequest Build()
    {
        if (menu is null) { throw AppError.Invalid("select a published menu"); }
        if (lines.Count == 0) { throw AppError.Invalid("order must have at least one item"); }
        return new PlaceOrderRequest(
            menu.Id,
            lines.Select(static line => new PlaceOrderItem(line.Drink.Id, line.Quantity, line.Notes)).ToArray(),
            orderNotes).Normalize();
    }

    public TagCollection? DesiredTags() => string.IsNullOrWhiteSpace(tags)
        ? null
        : TagCollection.Parse(tags.Trim());

    public string Render()
    {
        List<string> output =
        [
            "Place Order",
            string.Empty,
            $"{Marker(OrderPlacementField.Menu)}Search menus: {menuQuery}",
        ];
        if (!loaded) { output.Add("Loading published menus..."); }
        else if (visibleMenus.Count == 0) { output.Add("No matching published menus"); }
        else
        {
            foreach ((OrderMenuOption option, int index) in visibleMenus.Take(6).Select((value, index) => (value, index)))
            {
                output.Add($"{(index == menuIndex ? ">" : " ")} {option.Name}");
            }
        }

        if (menu is not null)
        {
            output.Add($"Menu: {menu.Name}");
            output.Add($"{Marker(OrderPlacementField.Drink)}Search drinks: {drinkQuery}");
            foreach ((OrderDrinkOption option, int index) in visibleDrinks.Take(6).Select((value, index) => (value, index)))
            {
                output.Add($"{(index == drinkIndex ? ">" : " ")} {option.Name}");
            }

            output.Add($"{Marker(OrderPlacementField.Quantity)}Quantity: {quantity}");
            output.Add($"{Marker(OrderPlacementField.ItemNotes)}Item notes: {itemNotes}");
            foreach (OrderPlacementLine line in lines)
            {
                output.Add($"- {line.Drink.Name} × {line.Quantity}{(line.Notes.Length == 0 ? string.Empty : $" · {line.Notes}")}");
            }

            output.Add($"{Marker(OrderPlacementField.OrderNotes)}Order notes: {orderNotes}");
            output.Add($"{Marker(OrderPlacementField.Tags)}Complete tags (optional): {tags}");
        }

        if (Saving) { output.Add("Saving..."); }
        if (Error is not null) { output.Add($"Error: {Error}"); }
        output.Add("[Tab] field · [j/k] choose · [Enter] select/add · [Ctrl+S] place · [Esc] back");
        return string.Join('\n', output);
    }

    private string Marker(OrderPlacementField field) => Field == field ? "> " : "  ";

    private string Value(OrderPlacementField field) => field switch
    {
        OrderPlacementField.Menu => menuQuery,
        OrderPlacementField.Drink => drinkQuery,
        OrderPlacementField.Quantity => quantity,
        OrderPlacementField.ItemNotes => itemNotes,
        OrderPlacementField.OrderNotes => orderNotes,
        OrderPlacementField.Tags => tags,
        _ => string.Empty,
    };

    private void FilterMenus()
    {
        visibleMenus = menus.Where(option => menuQuery.Length == 0
                || option.Name.Contains(menuQuery, StringComparison.OrdinalIgnoreCase)
                || option.Id.Value.Contains(menuQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
        menuIndex = Math.Clamp(menuIndex, 0, Math.Max(visibleMenus.Count - 1, 0));
    }

    private void FilterDrinks()
    {
        visibleDrinks = (menu?.Drinks ?? []).Where(option => drinkQuery.Length == 0
                || option.Name.Contains(drinkQuery, StringComparison.OrdinalIgnoreCase)
                || option.Id.Value.Contains(drinkQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
        drinkIndex = Math.Clamp(drinkIndex, 0, Math.Max(visibleDrinks.Count - 1, 0));
    }

    private static string Chop(string value) => value.Length == 0 ? value : value[..^1];
}
