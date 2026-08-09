using System.Globalization;
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
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;

namespace Mixology.Tui.Workspaces;

public enum IngredientsWorkspaceMode
{
    Browse,
    Filter,
    Create,
    Edit,
    Retire,
    Submitting,
}

public interface IIngredientsWorkspaceOperations
{
    Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken cancellationToken);
    Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Ingredient? selected, CancellationToken cancellationToken);
    Task<Ingredient> CreateAsync(
        CreateIngredientRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken);
    Task<Ingredient> UpdateAsync(
        UpdateIngredientRequest request,
        TagCollection? desiredTags,
        CancellationToken cancellationToken);
    Task<Ingredient> RetireAsync(RetireIngredientRequest request, CancellationToken cancellationToken);
}

public sealed class IngredientsWorkspace : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    private const string TagsField = "Complete tags (optional)";
    private readonly object sync = new();
    private readonly IIngredientsWorkspaceOperations operations;
    private readonly WorkspaceRequestTracker requests = new();
    private readonly TableModel<Ingredient, IngredientId> table = new(
        static ingredient => ingredient.Id,
        [
            new("Name", static ingredient => ingredient.Name),
            new("Category", static ingredient => ingredient.Category.Value),
            new("Unit", static ingredient => ingredient.Unit.Value),
        ]);
    private readonly List<Cursor> history = [];
    private Dictionary<ActionId, ActionState> actions = [];
    private ListIngredientsRequest request = new();
    private CancellationTokenSource? listCancellation;
    private CancellationTokenSource? detailCancellation;
    private Ingredient? detail;
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
    private IngredientsWorkspaceMode submitOrigin;

    public IngredientsWorkspace(IIngredientsWorkspaceOperations operations)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public WorkspaceId Id => NavigationProjector.IngredientsWorkspace;
    public string Title => "Ingredients";
    public IngredientsWorkspaceMode Mode { get; private set; }
    public InputOwnership InputOwnership => Mode == IngredientsWorkspaceMode.Browse
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
    public IReadOnlyList<Ingredient> Rows => table.Rows;
    public Ingredient? Selected
    {
        get
        {
            lock (sync)
            {
                return table.TryGetSelected(out Ingredient? selected) ? selected : null;
            }
        }
    }
    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        IngredientsModule ingredients,
        IngredientActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new IngredientsWorkspace(
            new ModuleOperations(ingredients, projector, taggedMutations, session, actor));
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

        if (Mode == IngredientsWorkspaceMode.Submitting)
        {
            return true;
        }

        if (Mode != IngredientsWorkspaceMode.Browse)
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
            case 'c':
                StartCreate();
                return true;
            case 'e':
                StartEdit();
                return true;
            case 'd':
            case 'R':
                StartRetire();
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
                IngredientsWorkspaceMode.Browse when showFilterHelp => RenderFilterHelp(),
                IngredientsWorkspaceMode.Browse => RenderBrowse(viewport),
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
        ListIngredientsRequest snapshot;
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
        ListIngredientsRequest snapshot,
        CancellationTokenSource source)
    {
        try
        {
            Page<Ingredient> page = await operations.ListAsync(snapshot, source.Token).ConfigureAwait(false);
            IngredientId? selected;
            lock (sync)
            {
                if (disposed || generation != listGeneration)
                {
                    return;
                }

                selected = table.TryGetSelected(out Ingredient? current) ? current?.Id : null;
                table.Replace(page.Items);
                if (selected is { } id)
                {
                    int index = table.Rows.ToList().FindIndex(item => item.Id == id);
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
                    loadError = Safe(exception, "load ingredients workspace");
                    loading = false;
                }
            }

            Changed?.Invoke();
        }
    }

    private void StartDetail()
    {
        Ingredient? selected;
        lock (sync)
        {
            selected = table.TryGetSelected(out Ingredient? value) ? value : null;
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
        Ingredient? selected,
        CancellationTokenSource source)
    {
        try
        {
            Ingredient? loaded = selected is null
                ? null
                : await operations.GetAsync(selected.Id, source.Token).ConfigureAwait(false);
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(loaded, source.Token)
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
                    actionError = Safe(exception, "load ingredient detail");
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
            if (next.IsEmpty || !Enabled(IngredientActionProjector.ListAction))
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
            if (history.Count == 0 || !Enabled(IngredientActionProjector.ListAction))
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
            if (!Enabled(IngredientActionProjector.ListAction))
            {
                return;
            }

            Mode = IngredientsWorkspaceMode.Filter;
            form = new WorkspaceForm(
            [
                new FormField("Category", request.Category?.Value ?? string.Empty, ValidateOptionalCategory),
                new FormField("Expression", request.Filter ?? string.Empty),
                new FormField("Page size", request.EffectiveLimit.ToString(CultureInfo.InvariantCulture), ValidatePositiveInteger),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartCreate()
    {
        lock (sync)
        {
            if (!Enabled(IngredientActionProjector.CreateAction))
            {
                return;
            }

            Mode = IngredientsWorkspaceMode.Create;
            form = IngredientForm(null);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartEdit()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(IngredientActionProjector.EditAction))
            {
                return;
            }

            Mode = IngredientsWorkspaceMode.Edit;
            form = IngredientForm(detail);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartRetire()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(IngredientActionProjector.RetireAction))
            {
                return;
            }

            Mode = IngredientsWorkspaceMode.Retire;
            form = new WorkspaceForm(
            [
                new FormField("Replacement ingredient ID"),
                new FormField("Replacement ratio", "1"),
            ]);
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void SubmitForm()
    {
        WorkspaceForm active;
        IngredientsWorkspaceMode origin;
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

        if (origin == IngredientsWorkspaceMode.Filter)
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

        Func<CancellationToken, Task<Ingredient>> mutation;
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
            Mode = IngredientsWorkspaceMode.Submitting;
            mutationError = null;
        }

        Changed?.Invoke();
        _ = requests.Track(RunMutationAsync(mutation, active, source));
    }

    private void ApplyFilter(WorkspaceForm active)
    {
        IngredientCategory? category = string.IsNullOrWhiteSpace(active["Category"])
            ? null
            : IngredientCategory.Parse(active["Category"]);
        int limit = int.Parse(active["Page size"], CultureInfo.InvariantCulture);
        active.Model.CompleteSubmit();
        lock (sync)
        {
            request = new ListIngredientsRequest(category, active["Expression"].Trim(), default, limit);
            history.Clear();
            next = default;
            Mode = IngredientsWorkspaceMode.Browse;
            form = null;
            showFilterHelp = false;
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private Func<CancellationToken, Task<Ingredient>> BuildMutation(
        IngredientsWorkspaceMode origin,
        WorkspaceForm active)
    {
        return origin switch
        {
            IngredientsWorkspaceMode.Create => token => operations.CreateAsync(
                new CreateIngredientRequest(
                    active["Name"],
                    IngredientCategory.Parse(active["Category"]),
                    Unit.Parse(active["Unit"]),
                    active["Description"]),
                active.DesiredTags(TagsField),
                token),
            IngredientsWorkspaceMode.Edit when detail is not null => token => operations.UpdateAsync(
                new UpdateIngredientRequest(
                    detail.Id,
                    active["Name"],
                    IngredientCategory.Parse(active["Category"]),
                    Unit.Parse(active["Unit"]),
                    active["Description"]),
                active.DesiredTags(TagsField),
                token),
            IngredientsWorkspaceMode.Retire when detail is not null => token => operations.RetireAsync(
                new RetireIngredientRequest(detail.Id, ParseRetirement(active)), token),
            _ => throw AppError.FailedPrecondition("ingredient form has no target"),
        };
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<Ingredient>> mutation,
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

                Mode = IngredientsWorkspaceMode.Browse;
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
            Exception safe = Safe(exception, "mutate ingredient from TUI");
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
            Mode = IngredientsWorkspaceMode.Browse;
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
            $"Ingredients · page {history.Count + 1} · size {request.EffectiveLimit}",
            loading ? "Loading..." : $"{table.Rows.Count} result(s)",
            string.Empty,
        ];
        int rowLimit = Math.Max(viewport.Height - 8, 1);
        foreach ((Ingredient ingredient, int index) in table.Rows.Take(rowLimit).Select((value, index) => (value, index)))
        {
            string marker = index == table.SelectedIndex ? ">" : " ";
            list.Add($"{marker} {ingredient.Name} · {ingredient.Category} · {ingredient.Unit}");
        }

        List<string> selected = detail is null
            ? ["Select an ingredient to view details"]
            :
            [
                detail.Name,
                $"ID: {detail.Id}",
                $"Category: {detail.Category}",
                $"Unit: {detail.Unit}",
                $"Tags: {(detail.Tags.Count == 0 ? "(none)" : detail.Tags.Format())}",
                string.Empty,
                string.IsNullOrWhiteSpace(detail.Description) ? string.Empty : "Description:",
                detail.Description,
            ];
        string body = WorkspaceRender.TwoPane(list, selected, viewport.Width);
        return string.Join('\n',
            body,
            string.Empty,
            BrowseHelp());
    }

    private string BrowseHelp()
    {
        List<string> keys = ["[j/k] select", "[f] filter", "[h] filter help", "[[/]] page", "[r] refresh"];
        if (Enabled(IngredientActionProjector.CreateAction))
        {
            keys.Add("[c] create");
        }

        if (Enabled(IngredientActionProjector.EditAction))
        {
            keys.Add("[e] edit");
        }

        if (Enabled(IngredientActionProjector.RetireAction))
        {
            keys.Add("[d/R] retire");
        }

        return string.Join("  ", keys);
    }

    private static string RenderFilterHelp() => """
        Ingredient filter help · [h] close

        Fields: id, name, category, unit, description, tags
        Comparisons: == != < <= > >= in not in
        Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches

        category == "spirit" && name.contains("gin")
        unit in ["ml", "oz"] && !description.contains("seasonal")
        tags contains "featured" || tags contains "region=west"
        """;

    private string FormTitle() => Mode switch
    {
        IngredientsWorkspaceMode.Filter => "Filter Ingredients",
        IngredientsWorkspaceMode.Create => "Create Ingredient",
        IngredientsWorkspaceMode.Edit => $"Edit Ingredient: {detail?.Name}",
        IngredientsWorkspaceMode.Retire => $"Retire Ingredient: {detail?.Name}",
        IngredientsWorkspaceMode.Submitting => "Submitting ingredient mutation...",
        _ => "Ingredients",
    };

    private string FormFooter() => Mode == IngredientsWorkspaceMode.Submitting
        ? "Submitting..."
        : "[Tab] next field · [Ctrl+S] submit · [Esc] cancel";

    private static WorkspaceForm IngredientForm(Ingredient? ingredient) => new(
    [
        new FormField("Name", ingredient?.Name ?? string.Empty, ValidateName),
        new FormField("Category", ingredient?.Category.Value ?? IngredientCategory.Spirit.Value, ValidateCategory),
        new FormField("Unit", ingredient?.Unit.Value ?? Unit.Ounce.Value, ValidateUnit),
        new FormField("Description", ingredient?.Description ?? string.Empty, ValidateDescription),
        new FormField(TagsField, ingredient?.Tags.Format() ?? string.Empty),
    ]);

    private static Retirement ParseRetirement(WorkspaceForm active)
    {
        string replacement = active["Replacement ingredient ID"].Trim();
        if (replacement.Length == 0)
        {
            return new Retirement();
        }

        if (!double.TryParse(
            active["Replacement ratio"],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double ratio))
        {
            throw AppError.Invalid("replacement ratio must be a number");
        }

        return new Retirement(IngredientId.Parse(replacement), ratio).Normalize();
    }

    private static string? ValidateName(string value) => string.IsNullOrWhiteSpace(value)
        ? "name is required"
        : value.Trim().Length > 100 ? "name must be at most 100 characters" : null;

    private static string? ValidateDescription(string value) => value.Trim().Length > 500
        ? "description must be at most 500 characters"
        : null;

    private static string? ValidateCategory(string value) =>
        IngredientCategory.TryParse(value, out _) ? null : "category is invalid";

    private static string? ValidateOptionalCategory(string value) => string.IsNullOrWhiteSpace(value)
        ? null
        : ValidateCategory(value);

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

    private static string? ValidatePositiveInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? null
            : "page size must be greater than zero";

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        IngredientsModule ingredients,
        IngredientActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor) : IIngredientsWorkspaceOperations
    {
        public Task<Page<Ingredient>> ListAsync(
            ListIngredientsRequest request,
            CancellationToken cancellationToken) =>
            ingredients.ListAsync(session, request, cancellationToken);

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken) =>
            ingredients.GetAsync(session, id, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            Ingredient? selected,
            CancellationToken cancellationToken) =>
            projector.ProjectAsync(actor, selected, cancellationToken);

        public Task<Ingredient> CreateAsync(
            CreateIngredientRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                (active, token) => ingredients.CreateAsync(active, request, token),
                desiredTags,
                static ingredient => ingredient.EntityUid,
                static (ingredient, tags) => ingredient with { Tags = tags },
                cancellationToken);

        public Task<Ingredient> UpdateAsync(
            UpdateIngredientRequest request,
            TagCollection? desiredTags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                (active, token) => ingredients.UpdateAsync(active, request, token),
                desiredTags,
                static ingredient => ingredient.EntityUid,
                static (ingredient, tags) => ingredient with { Tags = tags },
                cancellationToken);

        public Task<Ingredient> RetireAsync(
            RetireIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.RetireAsync(session, request, cancellationToken);
    }
}
