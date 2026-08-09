using System.Globalization;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Menus.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;

namespace Mixology.Tui.Workspaces.Menus;

public enum MenusWorkspaceMode
{
    Browse,
    Filter,
    Create,
    Edit,
    Delete,
    AddDrink,
    RemoveDrink,
    Publish,
    Draft,
    Analyze,
    Submitting,
}

public sealed record MenuDrinkOption(DrinkId Id, string Name);

public interface IMenusWorkspaceOperations
{
    Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken);
    Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Menu? selected, CancellationToken cancellationToken);
    Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken);
    Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken);
    Task<IReadOnlyList<MenuDrinkOption>> DrinkCatalogAsync(CancellationToken cancellationToken);
    Task<Menu> CreateAsync(CreateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> UpdateAsync(UpdateMenuRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken);
    Task<Menu> AddDrinkAsync(AddMenuItemRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> PublishAsync(MenuId id, TagCollection? tags, CancellationToken cancellationToken);
    Task<Menu> DraftAsync(MenuId id, TagCollection? tags, CancellationToken cancellationToken);
}

public sealed class MenusWorkspace : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    private const string TagsField = "Complete tags (optional)";
    private readonly object sync = new();
    private readonly IMenusWorkspaceOperations operations;
    private readonly WorkspaceRequestTracker requests = new();
    private readonly TableModel<Menu, MenuId> table = new(
        static menu => menu.Id,
        [
            new("Name", static menu => menu.Name),
            new("Status", static menu => menu.Status.Value),
            new("Drinks", static menu => menu.Items.Count.ToString(CultureInfo.InvariantCulture)),
        ]);
    private readonly List<Cursor> history = [];
    private Dictionary<ActionId, ActionState> actions = [];
    private ListMenusRequest request = new();
    private CancellationTokenSource? listCancellation;
    private CancellationTokenSource? detailCancellation;
    private CancellationTokenSource? workflowCancellation;
    private Menu? detail;
    private ReadinessReport? readiness;
    private MenuAnalysis? analysis;
    private WorkspaceForm? form;
    private MenuDrinkPicker? picker;
    private Exception? loadError;
    private Exception? actionError;
    private Exception? mutationError;
    private Cursor next;
    private long listGeneration;
    private long detailGeneration;
    private long workflowGeneration;
    private bool loading;
    private bool analysisLoading;
    private bool showFilterHelp;
    private bool disposed;
    private MenusWorkspaceMode submitOrigin;

    public MenusWorkspace(IMenusWorkspaceOperations operations)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public WorkspaceId Id => NavigationProjector.MenusWorkspace;
    public string Title => "Menus";
    public MenusWorkspaceMode Mode { get; private set; }
    public InputOwnership InputOwnership => Mode == MenusWorkspaceMode.Browse
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

    public IReadOnlyList<Menu> Rows => table.Rows;
    public Menu? Selected
    {
        get { lock (sync) { return table.TryGetSelected(out Menu? value) ? value : null; } }
    }

    public ReadinessReport? Readiness { get { lock (sync) { return readiness; } } }
    public MenuAnalysis? Analysis { get { lock (sync) { return analysis; } } }
    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        MenusModule menus,
        DrinksModule drinks,
        MenuActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(drinks);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new MenusWorkspace(
            new ModuleOperations(menus, drinks, projector, taggedMutations, session, actor));
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
        if (Mode == MenusWorkspaceMode.Submitting)
        {
            return true;
        }

        if (Mode is MenusWorkspaceMode.Delete or MenusWorkspaceMode.Publish or MenusWorkspaceMode.Draft)
        {
            return HandleConfirmation(key);
        }

        if (Mode is MenusWorkspaceMode.AddDrink or MenusWorkspaceMode.RemoveDrink)
        {
            return HandlePicker(key);
        }

        if (Mode != MenusWorkspaceMode.Browse)
        {
            if (key == '\u001b')
            {
                CancelWorkflow();
                return true;
            }

            if (Mode == MenusWorkspaceMode.Analyze && analysisLoading)
            {
                return true;
            }

            if (key is SubmitKey or '\r')
            {
                SubmitForm();
                return true;
            }

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
            case 'c': StartMenuForm(MenusWorkspaceMode.Create); return true;
            case 'e': StartMenuForm(MenusWorkspaceMode.Edit); return true;
            case 'd': StartConfirmation(MenusWorkspaceMode.Delete, MenuActionProjector.DeleteAction); return true;
            case 'a': StartDrinkPicker(remove: false); return true;
            case 'x': StartDrinkPicker(remove: true); return true;
            case 'p': StartConfirmation(MenusWorkspaceMode.Publish, MenuActionProjector.PublishAction); return true;
            case 'u': StartConfirmation(MenusWorkspaceMode.Draft, MenuActionProjector.DraftAction); return true;
            case 'y': StartAnalysis(); return true;
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
                MenusWorkspaceMode.Browse when showFilterHelp => RenderFilterHelp(),
                MenusWorkspaceMode.Browse => RenderBrowse(viewport),
                MenusWorkspaceMode.Delete => Confirmation("Delete", "This permanently archives the draft."),
                MenusWorkspaceMode.Publish => Confirmation("Publish", ReadinessSummary()),
                MenusWorkspaceMode.Draft => Confirmation("Return to draft", "Orders can no longer be placed from it."),
                MenusWorkspaceMode.AddDrink => picker?.Render("Add drink to menu") ?? "Loading drinks...",
                MenusWorkspaceMode.RemoveDrink => picker?.Render("Remove drink from menu") ?? "Loading drinks...",
                MenusWorkspaceMode.Analyze => RenderAnalysis(),
                _ => form?.Render(FormTitle(), FormFooter()) ?? "Loading form...",
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
        long generation;
        ListMenusRequest snapshot;
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

    private async Task LoadListAsync(long generation, ListMenusRequest snapshot, CancellationTokenSource source)
    {
        try
        {
            Page<Menu> page = await operations.ListAsync(snapshot, source.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != listGeneration) { return; }
                MenuId? selected = table.TryGetSelected(out Menu? current) ? current?.Id : null;
                table.Replace(page.Items);
                if (selected is { } id)
                {
                    int index = table.Rows.ToList().FindIndex(item => item.Id == id);
                    if (index >= 0) { table.Select(index); }
                }

                next = page.Next;
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
                    loadError = Safe(exception, "load menus workspace");
                    loading = false;
                }
            }

            Changed?.Invoke();
        }
    }

    private void StartDetail()
    {
        Menu? selected;
        lock (sync) { selected = table.TryGetSelected(out Menu? value) ? value : null; }
        CancellationTokenSource source = requests.Create(CancellationToken.None);
        CancellationTokenSource? previous;
        long generation;
        lock (sync)
        {
            previous = detailCancellation;
            detailCancellation = source;
            generation = ++detailGeneration;
            detail = null;
            readiness = null;
            actions = [];
            actionError = null;
        }

        previous?.Cancel();
        _ = requests.Track(LoadDetailAsync(generation, selected, source));
    }

    private async Task LoadDetailAsync(long generation, Menu? selected, CancellationTokenSource source)
    {
        try
        {
            Menu? loaded = selected is null
                ? null
                : await operations.GetAsync(selected.Id, source.Token).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(loaded, source.Token)
                .ConfigureAwait(false);
            ReadinessReport? report = null;
            if (loaded is not null && projected.Any(state =>
                    state.Id == MenuActionProjector.ReadinessAction && state.Visible && state.Enabled))
            {
                report = await operations.ReadinessAsync(loaded.Id, source.Token).ConfigureAwait(false);
                projected = MenuActionProjector.ApplyReadiness(projected, report);
            }

            lock (sync)
            {
                if (disposed || generation != detailGeneration) { return; }
                detail = loaded;
                readiness = report;
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
                    readiness = null;
                    actions = [];
                    actionError = Safe(exception, "load menu detail");
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
            if (next.IsEmpty || !Enabled(MenuActionProjector.ListAction)) { return; }
            history.Add(request.Cursor);
            request = request with { Cursor = next };
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void PreviousPage()
    {
        lock (sync)
        {
            if (history.Count == 0 || !Enabled(MenuActionProjector.ListAction)) { return; }
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
            if (!Enabled(MenuActionProjector.ListAction)) { return; }
            Mode = MenusWorkspaceMode.Filter;
            form = new WorkspaceForm(
            [
                new FormField("Status", request.Status?.Value ?? string.Empty, ValidateOptionalStatus),
                new FormField("Expression", request.Filter ?? string.Empty),
                new FormField("Page size", request.EffectiveLimit.ToString(CultureInfo.InvariantCulture), ValidatePositiveInteger),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartMenuForm(MenusWorkspaceMode mode)
    {
        lock (sync)
        {
            ActionId action = mode == MenusWorkspaceMode.Create
                ? MenuActionProjector.CreateAction
                : MenuActionProjector.EditAction;
            if (!Enabled(action) || (mode == MenusWorkspaceMode.Edit && detail is null)) { return; }
            Mode = mode;
            form = new WorkspaceForm(
            [
                new FormField("Name", mode == MenusWorkspaceMode.Edit ? detail!.Name : string.Empty, ValidateName),
                new FormField("Description", mode == MenusWorkspaceMode.Edit ? detail!.Description : string.Empty),
                new FormField(TagsField, mode == MenusWorkspaceMode.Edit ? detail!.Tags.Format() : string.Empty),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartConfirmation(MenusWorkspaceMode mode, ActionId action)
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
        if (key == '\u001b')
        {
            CancelWorkflow();
        }
        else if (key == '\t')
        {
            lock (sync) { _ = form?.Handle(key); }
            Changed?.Invoke();
        }
        else if (key is SubmitKey or '\r')
        {
            SubmitConfirmation();
        }
        else if (!char.IsControl(key))
        {
            lock (sync) { _ = form?.Handle(key); }
            Changed?.Invoke();
        }

        return true;
    }

    private void StartDrinkPicker(bool remove)
    {
        Menu? target;
        ActionId action = remove ? MenuActionProjector.RemoveDrinkAction : MenuActionProjector.AddDrinkAction;
        lock (sync)
        {
            if (detail is null || !Enabled(action)) { return; }
            target = detail;
            Mode = remove ? MenusWorkspaceMode.RemoveDrink : MenusWorkspaceMode.AddDrink;
            picker = new MenuDrinkPicker(target.Tags.Format());
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
        _ = requests.Track(LoadPickerAsync(generation, target, remove, source));
    }

    private async Task LoadPickerAsync(
        long generation,
        Menu target,
        bool remove,
        CancellationTokenSource source)
    {
        try
        {
            IReadOnlyList<MenuDrinkOption> catalog = await operations.DrinkCatalogAsync(source.Token)
                .ConfigureAwait(false);
            HashSet<DrinkId> included = target.Items.Select(static item => item.DrinkId).ToHashSet();
            MenuDrinkOption[] choices = catalog
                .Where(option => remove ? included.Contains(option.Id) : !included.Contains(option.Id))
                .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static option => option.Id.Value, StringComparer.Ordinal)
                .ToArray();
            lock (sync)
            {
                if (disposed || generation != workflowGeneration || picker is null) { return; }
                picker.SetChoices(choices);
            }

            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && generation == workflowGeneration)
                {
                    mutationError = Safe(exception, "load menu drink picker");
                    picker?.SetError(TuiErrorAdapter.Adapt(mutationError).Message);
                }
            }

            Changed?.Invoke();
        }
    }

    private bool HandlePicker(char key)
    {
        if (key == '\u001b')
        {
            CancelWorkflow();
            return true;
        }

        if (key is SubmitKey or '\r')
        {
            SubmitPicker();
            return true;
        }

        lock (sync) { picker?.Handle(key); }
        Changed?.Invoke();
        return true;
    }

    private void StartAnalysis()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(MenuActionProjector.ReadinessAction)) { return; }
            Mode = MenusWorkspaceMode.Analyze;
            analysis = null;
            form = new WorkspaceForm([new FormField("Target margin", "0.70", ValidateMargin)]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void SubmitForm()
    {
        WorkspaceForm active;
        MenusWorkspaceMode origin;
        lock (sync)
        {
            if (form is null || !form.Model.TryBeginSubmit()) { Changed?.Invoke(); return; }
            active = form;
            origin = Mode;
        }

        if (origin == MenusWorkspaceMode.Filter)
        {
            ApplyFilter(active);
            return;
        }

        if (origin == MenusWorkspaceMode.Analyze)
        {
            StartAnalyzeRequest(active);
            return;
        }

        Func<CancellationToken, Task<Menu>> mutation;
        try
        {
            mutation = origin switch
            {
                MenusWorkspaceMode.Create => token => operations.CreateAsync(
                    new CreateMenuRequest(active["Name"], active["Description"]),
                    active.DesiredTags(TagsField), token),
                MenusWorkspaceMode.Edit when detail is not null => token => operations.UpdateAsync(
                    new UpdateMenuRequest(detail.Id, active["Name"], active["Description"]),
                    active.DesiredTags(TagsField), token),
                _ => throw AppError.FailedPrecondition("menu form has no target"),
            };
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "build menu mutation")).Message);
            Changed?.Invoke();
            return;
        }

        StartMutation(origin, mutation, active);
    }

    private void ApplyFilter(WorkspaceForm active)
    {
        try
        {
            MenuStatus? status = string.IsNullOrWhiteSpace(active["Status"])
                ? null
                : MenuStatus.Parse(active["Status"]);
            int limit = int.Parse(active["Page size"], CultureInfo.InvariantCulture);
            active.Model.CompleteSubmit();
            lock (sync)
            {
                request = new ListMenusRequest(status, active["Expression"], default, limit).Normalize();
                history.Clear();
                next = default;
                Mode = MenusWorkspaceMode.Browse;
                form = null;
                showFilterHelp = false;
            }

            _ = StartListAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "apply menu filter")).Message);
            Changed?.Invoke();
        }
    }

    private void StartAnalyzeRequest(WorkspaceForm active)
    {
        double margin;
        Menu target;
        try
        {
            margin = double.Parse(active["Target margin"], CultureInfo.InvariantCulture);
            target = detail ?? throw AppError.FailedPrecondition("menu analysis has no target");
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "build menu analysis")).Message);
            Changed?.Invoke();
            return;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        CancellationTokenSource? previous;
        long generation;
        lock (sync)
        {
            previous = workflowCancellation;
            workflowCancellation = source;
            generation = ++workflowGeneration;
            analysisLoading = true;
        }

        previous?.Cancel();
        _ = requests.Track(RunAnalysisAsync(generation, target.Id, margin, active, source));
        Changed?.Invoke();
    }

    private async Task RunAnalysisAsync(
        long generation,
        MenuId id,
        double margin,
        WorkspaceForm active,
        CancellationTokenSource source)
    {
        try
        {
            MenuAnalysis result = await operations.AnalyzeAsync(id, margin, source.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != workflowGeneration || Mode != MenusWorkspaceMode.Analyze) { return; }
                analysis = result;
                analysisLoading = false;
                active.Model.CompleteSubmit();
                active.Model.BeginEdit();
            }

            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
        catch (Exception exception)
        {
            Exception safe = Safe(exception, "analyze menu from TUI");
            lock (sync)
            {
                if (!disposed && generation == workflowGeneration)
                {
                    analysisLoading = false;
                    mutationError = safe;
                    active.Model.FailSubmit(TuiErrorAdapter.Adapt(safe).Message);
                }
            }

            Changed?.Invoke();
        }
    }

    private void SubmitConfirmation()
    {
        Menu target;
        MenusWorkspaceMode origin;
        WorkspaceForm active;
        lock (sync)
        {
            if (detail is null || form is null || !form.Model.TryBeginSubmit()) { return; }
            target = detail;
            origin = Mode;
            active = form;
        }

        Func<CancellationToken, Task<Menu>> mutation;
        try
        {
            TagCollection? tags = active.DesiredTags(TagsField);
            mutation = origin switch
            {
                MenusWorkspaceMode.Delete => token => operations.DeleteAsync(target.Id, token),
                MenusWorkspaceMode.Publish => token => operations.PublishAsync(target.Id, tags, token),
                MenusWorkspaceMode.Draft => token => operations.DraftAsync(target.Id, tags, token),
                _ => throw AppError.FailedPrecondition("menu confirmation has no target"),
            };
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "build menu lifecycle mutation")).Message);
            Changed?.Invoke();
            return;
        }

        StartMutation(origin, mutation, active);
    }

    private void SubmitPicker()
    {
        Menu target;
        MenuDrinkOption? choice;
        TagCollection? tags = null;
        MenusWorkspaceMode origin;
        lock (sync)
        {
            if (detail is null || picker is null || picker.Saving) { return; }
            target = detail;
            choice = picker.Selected;
            if (choice is null)
            {
                picker.SetError("select a drink");
            }
            try { tags = choice is null ? null : picker.DesiredTags(); }
            catch (Exception exception)
            {
                mutationError = Safe(exception, "parse menu tags");
                picker.SetError(TuiErrorAdapter.Adapt(mutationError).Message);
                choice = null;
            }

            picker.Saving = choice is not null;
            origin = Mode;
        }

        if (choice is null)
        {
            Changed?.Invoke();
            return;
        }

        Func<CancellationToken, Task<Menu>> mutation = origin == MenusWorkspaceMode.AddDrink
            ? token => operations.AddDrinkAsync(new AddMenuItemRequest(target.Id, choice.Id), tags, token)
            : token => operations.RemoveDrinkAsync(new RemoveMenuItemRequest(target.Id, choice.Id), tags, token);
        StartMutation(origin, mutation, active: null);
    }

    private void StartMutation(
        MenusWorkspaceMode origin,
        Func<CancellationToken, Task<Menu>> mutation,
        WorkspaceForm? active)
    {
        CancellationTokenSource source = requests.Create(CancellationToken.None);
        lock (sync)
        {
            submitOrigin = origin;
            Mode = MenusWorkspaceMode.Submitting;
            mutationError = null;
        }

        Changed?.Invoke();
        _ = requests.Track(RunMutationAsync(mutation, active, source));
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<Menu>> mutation,
        WorkspaceForm? active,
        CancellationTokenSource source)
    {
        try
        {
            _ = await mutation(source.Token).ConfigureAwait(false);
            active?.Model.CompleteSubmit();
            lock (sync)
            {
                if (disposed) { return; }
                Mode = MenusWorkspaceMode.Browse;
                form = null;
                picker = null;
                analysis = null;
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
                    active?.Model.FailSubmit("operation cancelled");
                    if (picker is not null) { picker.Saving = false; }
                }
            }

            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Exception safe = Safe(exception, "mutate menu from TUI");
            lock (sync)
            {
                if (!disposed)
                {
                    Mode = submitOrigin;
                    mutationError = safe;
                    active?.Model.FailSubmit(TuiErrorAdapter.Adapt(safe).Message);
                    if (picker is not null)
                    {
                        picker.Saving = false;
                        picker.SetError(TuiErrorAdapter.Adapt(safe).Message);
                    }
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
            form = null;
            picker = null;
            analysis = null;
            analysisLoading = false;
            Mode = MenusWorkspaceMode.Browse;
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
            $"Menus · page {history.Count + 1} · size {request.EffectiveLimit}",
            loading ? "Loading..." : $"{table.Rows.Count} result(s)",
            string.Empty,
        ];
        int rowLimit = Math.Max(viewport.Height - 8, 1);
        foreach ((Menu menu, int index) in table.Rows.Take(rowLimit).Select((value, index) => (value, index)))
        {
            string marker = index == table.SelectedIndex ? ">" : " ";
            list.Add($"{marker} {menu.Name} · {menu.Status} · {menu.Items.Count} drink(s)");
        }

        List<string> selected = detail is null ? ["Select a menu to view details"] : DetailLines();
        List<string> footer = WrapHelp(BrowseHelp(), viewport.Width);
        string body = string.Join('\n', WorkspaceRender.TwoPane(list, selected, viewport.Width)
            .Split('\n').Take(Math.Max(viewport.Height - footer.Count - 1, 1)));
        return string.Join('\n', [body, string.Empty, .. footer]);
    }

    private List<string> DetailLines()
    {
        Menu menu = detail!;
        List<string> lines =
        [
            menu.Name,
            $"ID: {menu.Id}",
            $"Status: {menu.Status}",
            $"Tags: {(menu.Tags.Count == 0 ? "(none)" : menu.Tags.Format())}",
            $"Readiness: {ReadinessSummary()}",
        ];
        if (readiness is not null)
        {
            foreach (ReadinessFinding finding in readiness.Findings)
            {
                lines.Add($"- {finding.Severity} · {finding.Code}: {finding.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(menu.Description)) { lines.Add($"Description: {menu.Description}"); }
        AddDisabledReason(lines, MenuActionProjector.PublishAction, "Publish");
        AddDisabledReason(lines, MenuActionProjector.EditAction, "Edit");
        AddDisabledReason(lines, MenuActionProjector.DraftAction, "Return to draft");
        AddDisabledReason(lines, MenuActionProjector.RemoveDrinkAction, "Remove drink");
        lines.Add(string.Empty);
        lines.Add("Drinks:");
        foreach (MenuItem item in menu.Items.OrderBy(static item => item.SortOrder))
        {
            string name = item.DisplayName ?? item.DrinkId.Value;
            lines.Add($"- {name} · {item.Availability}");
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
        AddKey(keys, MenuActionProjector.CreateAction, "[c] create");
        AddKey(keys, MenuActionProjector.EditAction, "[e] edit");
        AddKey(keys, MenuActionProjector.DeleteAction, "[d] delete");
        AddKey(keys, MenuActionProjector.AddDrinkAction, "[a] add drink");
        AddKey(keys, MenuActionProjector.RemoveDrinkAction, "[x] remove drink");
        AddKey(keys, MenuActionProjector.PublishAction, "[p] publish");
        AddKey(keys, MenuActionProjector.DraftAction, "[u] draft");
        AddKey(keys, MenuActionProjector.ReadinessAction, "[y] analyze");
        return string.Join("  ", keys);
    }

    private void AddKey(List<string> keys, ActionId action, string label)
    {
        if (Enabled(action)) { keys.Add(label); }
    }

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

    private string Confirmation(string verb, string context) =>
        $"{verb} {detail?.Name}?\n\n{context}\n\n[Enter/Ctrl+S] confirm · [Esc] cancel";

    private string ReadinessSummary()
    {
        if (readiness is null) { return "not loaded"; }
        if (readiness.Findings.Count == 0) { return "ready"; }
        int blockers = readiness.Findings.Count(static finding => finding.Severity == ReadinessSeverity.Blocker);
        int warnings = readiness.Findings.Count(static finding => finding.Severity == ReadinessSeverity.Warning);
        return $"{blockers} blocker(s), {warnings} warning(s)";
    }

    private string RenderAnalysis()
    {
        string input = form?.Render("Menu cost and availability analysis", "[Ctrl+S] analyze · [Esc] back")
            ?? "Loading analysis...";
        if (analysis is null) { return input; }
        List<string> lines =
        [
            input,
            string.Empty,
            analysisLoading ? "Analyzing..." : string.Empty,
            $"Available: {analysis.AvailableCount}/{analysis.TotalCount}",
            $"Average margin: {(analysis.AverageMargin is { } average ? average.ToString("P0", CultureInfo.InvariantCulture) : "n/a")}",
        ];
        foreach (MenuItemAnalysis item in analysis.Items)
        {
            string cost = item.CostUnknown || item.Cost is null ? "unknown" : item.Cost.Value.ToString();
            string price = item.MenuPrice?.ToString() ?? (item.SuggestedPrice is { } suggested ? $"suggested {suggested}" : "n/a");
            string margin = item.Margin?.ToString("P0", CultureInfo.InvariantCulture) ?? "n/a";
            lines.Add($"{item.Name}: cost {cost} · price {price} · margin {margin} · {item.Availability}");
        }

        return string.Join('\n', lines);
    }

    private static string RenderFilterHelp() => """
        Menu filter help · [h] close

        Fields: id, name, description, status, tags
        Comparisons: == != < <= > >= in not in
        Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches

        status == "published" && name.contains("night")
        tags contains "featured" || description.contains("seasonal")
        """;

    private string FormTitle() => Mode switch
    {
        MenusWorkspaceMode.Filter => "Filter Menus",
        MenusWorkspaceMode.Create => "Create Menu",
        MenusWorkspaceMode.Edit => $"Edit Menu: {detail?.Name}",
        MenusWorkspaceMode.Submitting => "Submitting menu mutation...",
        _ => "Menus",
    };

    private string FormFooter() => Mode == MenusWorkspaceMode.Submitting
        ? "Submitting..."
        : "[Tab] next field · [Ctrl+S] submit · [Esc] cancel";

    private static string? ValidateName(string value) => string.IsNullOrWhiteSpace(value)
        ? "name is required"
        : null;

    private static string? ValidateOptionalStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        try { _ = MenuStatus.Parse(value); return null; }
        catch (InvalidError error) { return error.UserMessage; }
    }

    private static string? ValidatePositiveInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? null
            : "page size must be greater than zero";

    private static string? ValidateMargin(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
        && double.IsFinite(parsed) && parsed is > 0 and < 1
            ? null
            : "target margin must be a number between 0 and 1";

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        MenusModule menus,
        DrinksModule drinks,
        MenuActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor) : IMenusWorkspaceOperations
    {
        public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken) =>
            menus.ListAsync(session, request, cancellationToken);

        public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.GetAsync(session, id, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectAsync(Menu? selected, CancellationToken cancellationToken) =>
            projector.ProjectAsync(actor, selected, cancellationToken);

        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.ReadinessAsync(session, id, cancellationToken);

        public Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken) =>
            menus.AnalyzeAsync(session, id, targetMargin, cancellationToken);

        public async Task<IReadOnlyList<MenuDrinkOption>> DrinkCatalogAsync(CancellationToken cancellationToken)
        {
            List<MenuDrinkOption> values = [];
            Cursor cursor = default;
            do
            {
                Page<Drink> page = await drinks.ListAsync(
                    session,
                    new ListDrinksRequest(Cursor: cursor),
                    cancellationToken).ConfigureAwait(false);
                values.AddRange(page.Items.Select(static drink => new MenuDrinkOption(drink.Id, drink.Name)));
                cursor = page.Next;
            }
            while (!cursor.IsEmpty);
            return values;
        }

        public Task<Menu> CreateAsync(
            CreateMenuRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => menus.CreateAsync(active, request, token), tags, cancellationToken);

        public Task<Menu> UpdateAsync(
            UpdateMenuRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => menus.UpdateAsync(active, request, token), tags, cancellationToken);

        public Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.DeleteAsync(session, id, cancellationToken);

        public Task<Menu> AddDrinkAsync(
            AddMenuItemRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => menus.AddDrinkAsync(active, request, token), tags, cancellationToken);

        public Task<Menu> RemoveDrinkAsync(
            RemoveMenuItemRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => Tagged(
                (active, token) => menus.RemoveDrinkAsync(active, request, token), tags, cancellationToken);

        public Task<Menu> PublishAsync(MenuId id, TagCollection? tags, CancellationToken cancellationToken) =>
            Tagged((active, token) => menus.PublishAsync(active, id, token), tags, cancellationToken);

        public Task<Menu> DraftAsync(MenuId id, TagCollection? tags, CancellationToken cancellationToken) =>
            Tagged((active, token) => menus.DraftAsync(active, id, token), tags, cancellationToken);

        private Task<Menu> Tagged(
            Func<MixologySession, CancellationToken, Task<Menu>> mutate,
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

internal sealed class MenuDrinkPicker
{
    private readonly string baselineTags;
    private IReadOnlyList<MenuDrinkOption> all = [];
    private List<MenuDrinkOption> visible = [];
    private string query = string.Empty;
    private string tags;
    private int selected;
    private bool editTags;
    private bool loaded;

    public MenuDrinkPicker(string tags)
    {
        this.tags = tags;
        baselineTags = tags;
    }

    public MenuDrinkOption? Selected => selected >= 0 && selected < visible.Count ? visible[selected] : null;
    public bool Saving { get; set; }
    public string? Error { get; private set; }

    public void SetChoices(IReadOnlyList<MenuDrinkOption> choices)
    {
        all = choices;
        loaded = true;
        Filter();
    }

    public void SetError(string error) => Error = error;

    public void Handle(char key)
    {
        if (Saving) { return; }
        Error = null;
        switch (key)
        {
            case '\t': editTags = !editTags; break;
            case 'j' when !editTags: selected = Math.Min(selected + 1, Math.Max(visible.Count - 1, 0)); break;
            case 'k' when !editTags: selected = Math.Max(selected - 1, 0); break;
            case '\b':
            case '\u007f':
                if (editTags) { tags = Chop(tags); } else { query = Chop(query); Filter(); }
                break;
            default:
                if (!char.IsControl(key))
                {
                    if (editTags) { tags += key; } else { query += key; Filter(); }
                }
                break;
        }
    }

    public TagCollection? DesiredTags()
    {
        if (string.Equals(tags, baselineTags, StringComparison.Ordinal)) { return null; }
        try { return TagCollection.Parse(tags.Trim()); }
        catch (Exception exception) when (AppError.Find(exception) is not null)
        {
            throw AppError.Invalid($"invalid tags: {AppError.Find(exception)!.UserMessage}", exception);
        }
    }

    public string Render(string title)
    {
        List<string> lines =
        [
            title,
            string.Empty,
            $"{(editTags ? "  " : "> ")}Search: {query}",
            $"{(editTags ? "> " : "  ")}Complete tags (optional): {tags}",
            "[Tab] switch search/tags · [j/k] select",
            string.Empty,
        ];
        if (Saving) { lines.Add("Saving..."); }
        else if (Error is not null) { lines.Add($"Error: {Error}"); }
        else if (!loaded) { lines.Add("Loading drinks..."); }
        else if (visible.Count == 0) { lines.Add("No matching drinks"); }
        else
        {
            foreach ((MenuDrinkOption option, int index) in visible.Take(12).Select((value, index) => (value, index)))
            {
                lines.Add($"{(index == selected ? ">" : " ")} {option.Name} · {option.Id}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("[Enter/Ctrl+S] choose · [Esc] cancel");
        return string.Join('\n', lines);
    }

    private void Filter()
    {
        visible = all.Where(option => query.Length == 0
                || option.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.Id.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        selected = Math.Clamp(selected, 0, Math.Max(visible.Count - 1, 0));
    }

    private static string Chop(string value) => value.Length == 0 ? value : value[..^1];
}
