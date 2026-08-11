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
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;

namespace Mixology.Tui.Workspaces.Drinks;

public enum DrinksWorkspaceMode
{
    Browse,
    Filter,
    Create,
    Edit,
    Delete,
    Submitting,
}

public interface IDrinksWorkspaceOperations
{
    Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken);
    Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(Drink? selected, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(CancellationToken cancellationToken);
    Task<Drink> CreateAsync(CreateDrinkRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Drink> UpdateAsync(UpdateDrinkRequest request, TagCollection? tags, CancellationToken cancellationToken);
    Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken);
}

public sealed class DrinksWorkspace : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    public const char RecipeKey = '\u0012';
    public const char AddIngredientKey = '\u0001';
    public const char AddStepKey = '\u0014';
    public const char NextIngredientKey = '\u000a';
    public const char PreviousIngredientKey = '\u000b';
    public const char NextStepKey = '\u0015';
    public const char RemoveIngredientKey = '\u0004';
    public const char RemoveStepKey = '\u0018';
    private const string TagsField = "Complete tags (optional)";
    private readonly object sync = new();
    private readonly IDrinksWorkspaceOperations operations;
    private readonly WorkspaceRequestTracker requests = new();
    private readonly TableModel<Drink, DrinkId> table = new(
        static drink => drink.Id,
        [
            new("Name", static drink => drink.Name),
            new("Category", static drink => drink.Category.Value),
            new("Glass", static drink => drink.Glass.Value),
            new("Status", static drink => drink.Status.Value),
        ]);
    private readonly List<Cursor> history = [];
    private readonly List<DrinkRecipeEditor> editors = [];
    private Dictionary<ActionId, ActionState> actions = [];
    private ListDrinksRequest request = new();
    private CancellationTokenSource? listCancellation;
    private CancellationTokenSource? detailCancellation;
    private Drink? detail;
    private WorkspaceForm? form;
    private WorkspaceForm? recipeForm;
    private DrinkRecipeEditor? recipe;
    private Exception? loadError;
    private Exception? actionError;
    private Exception? mutationError;
    private Cursor next;
    private long listGeneration;
    private long detailGeneration;
    private bool loading;
    private bool showFilterHelp;
    private bool recipeInput;
    private int recipeIngredientIndex;
    private int recipeStepIndex;
    private bool disposed;
    private DrinksWorkspaceMode submitOrigin;

    public DrinksWorkspace(IDrinksWorkspaceOperations operations)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public WorkspaceId Id => NavigationProjector.DrinksWorkspace;
    public string Title => "Drinks";
    public DrinksWorkspaceMode Mode { get; private set; }
    public InputOwnership InputOwnership => Mode == DrinksWorkspaceMode.Browse
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

    public IReadOnlyList<Drink> Rows => table.Rows;
    public Drink? Selected
    {
        get { lock (sync) { return table.TryGetSelected(out Drink? value) ? value : null; } }
    }

    public DrinkRecipeEditor? RecipeEditor
    {
        get { lock (sync) { return recipe; } }
    }

    public bool RecipeInputActive
    {
        get { lock (sync) { return recipeInput; } }
    }

    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        DrinksModule drinks,
        IngredientsModule ingredients,
        DrinkActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(drinks);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(taggedMutations);
        ArgumentNullException.ThrowIfNull(session);
        return () => new DrinksWorkspace(
            new ModuleOperations(drinks, ingredients, projector, taggedMutations, session, actor));
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
        if (Mode == DrinksWorkspaceMode.Submitting)
        {
            return true;
        }

        if (Mode == DrinksWorkspaceMode.Delete)
        {
            if (key == '\u001b')
            {
                CancelForm();
            }
            else if (key is SubmitKey or '\r' or 'y' or 'Y')
            {
                SubmitDelete();
            }

            return true;
        }

        if (recipeInput)
        {
            return HandleRecipeInput(key);
        }

        if (Mode != DrinksWorkspaceMode.Browse)
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

            if (key == RecipeKey && Mode is DrinksWorkspaceMode.Create or DrinksWorkspaceMode.Edit)
            {
                BeginRecipeInput();
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
            case 'c': StartCreate(); return true;
            case 'e': StartEdit(); return true;
            case 'd': StartDelete(); return true;
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
                DrinksWorkspaceMode.Browse when showFilterHelp => RenderFilterHelp(),
                DrinksWorkspaceMode.Browse => RenderBrowse(viewport),
                DrinksWorkspaceMode.Delete => $"Delete {detail?.Name}?\n\n[Y/Enter] confirm · [Esc] cancel",
                _ when recipeInput => RenderRecipeInput(),
                _ => string.Join('\n',
                    form?.Render(FormTitle(), FormFooter()) ?? "Loading form...",
                    string.Empty,
                    recipe?.Render() ?? string.Empty),
            };
            return WorkspaceRender.Fit(content, viewport);
        }
    }

    public async ValueTask DisposeAsync()
    {
        DrinkRecipeEditor[] owned;
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            _ = ++listGeneration;
            _ = ++detailGeneration;
            owned = editors.ToArray();
        }

        await requests.DisposeAsync().ConfigureAwait(false);
        foreach (DrinkRecipeEditor editor in owned)
        {
            await editor.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Task StartListAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = requests.Create(cancellationToken);
        long generation;
        ListDrinksRequest snapshot;
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

    private async Task LoadListAsync(long generation, ListDrinksRequest snapshot, CancellationTokenSource source)
    {
        try
        {
            Page<Drink> page = await operations.ListAsync(snapshot, source.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || generation != listGeneration) { return; }
                DrinkId? selected = table.TryGetSelected(out Drink? current) ? current?.Id : null;
                table.Replace(page.Items);
                if (selected is { } id)
                {
                    int index = table.Rows.ToList().FindIndex(value => value.Id == id);
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
                    loadError = Safe(exception, "load drinks workspace");
                    loading = false;
                }
            }

            Changed?.Invoke();
        }
    }

    private void StartDetail()
    {
        Drink? selected;
        lock (sync) { selected = table.TryGetSelected(out Drink? value) ? value : null; }
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

    private async Task LoadDetailAsync(long generation, Drink? selected, CancellationTokenSource source)
    {
        try
        {
            Drink? loaded = selected is null
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
                    actionError = Safe(exception, "load drink detail");
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
            if (next.IsEmpty || !Enabled(DrinkActionProjector.ListAction)) { return; }
            history.Add(request.Cursor);
            request = request with { Cursor = next };
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void PreviousPage()
    {
        lock (sync)
        {
            if (history.Count == 0 || !Enabled(DrinkActionProjector.ListAction)) { return; }
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
            if (!Enabled(DrinkActionProjector.ListAction)) { return; }
            Mode = DrinksWorkspaceMode.Filter;
            form = new WorkspaceForm(
            [
                new FormField("Exact name", request.Name ?? string.Empty),
                new FormField("Category", request.Category?.Value ?? string.Empty, ValidateOptionalCategory),
                new FormField("Glass", request.Glass?.Value ?? string.Empty, ValidateOptionalGlass),
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
            if (!Enabled(DrinkActionProjector.CreateAction)) { return; }
            Mode = DrinksWorkspaceMode.Create;
            form = DrinkForm(null);
            recipe = OwnEditor(new DrinkRecipeEditor());
            mutationError = null;
            StartCatalog(recipe);
        }

        Changed?.Invoke();
    }

    private void StartEdit()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(DrinkActionProjector.EditAction)) { return; }
            Mode = DrinksWorkspaceMode.Edit;
            form = DrinkForm(detail);
            recipe = OwnEditor(new DrinkRecipeEditor(detail.Recipe));
            mutationError = null;
            StartCatalog(recipe);
        }

        Changed?.Invoke();
    }

    private void StartDelete()
    {
        lock (sync)
        {
            if (detail is null || !Enabled(DrinkActionProjector.DeleteAction)) { return; }
            Mode = DrinksWorkspaceMode.Delete;
            mutationError = null;
        }

        Changed?.Invoke();
    }

    private void StartCatalog(DrinkRecipeEditor editor)
    {
        editor.Changed += RaiseChanged;
        CancellationTokenSource source = requests.Create(CancellationToken.None);
        _ = requests.Track(editor.LoadCatalogAsync(operations.IngredientCatalogAsync, source.Token));
    }

    private DrinkRecipeEditor OwnEditor(DrinkRecipeEditor editor)
    {
        editors.Add(editor);
        return editor;
    }

    private void RaiseChanged() => Changed?.Invoke();

    private void BeginRecipeInput()
    {
        lock (sync)
        {
            if (recipe is null)
            {
                return;
            }

            recipeIngredientIndex = Math.Clamp(recipeIngredientIndex, 0, recipe.Ingredients.Count - 1);
            recipeStepIndex = Math.Clamp(recipeStepIndex, 0, recipe.Steps.Count - 1);
            recipeForm = BuildRecipeForm();
            recipeInput = true;
        }

        Changed?.Invoke();
    }

    private bool HandleRecipeInput(char key)
    {
        if (key == '\u001b')
        {
            lock (sync)
            {
                recipeInput = false;
                recipeForm = null;
            }

            Changed?.Invoke();
            return true;
        }

        if (key == SubmitKey)
        {
            ApplyRecipeAndExit();
            return true;
        }

        if (key is AddIngredientKey or AddStepKey or NextIngredientKey or PreviousIngredientKey
            or NextStepKey or RemoveIngredientKey or RemoveStepKey)
        {
            ApplyRecipeCommand(key);
            return true;
        }

        lock (sync) { _ = recipeForm?.Handle(key); }
        Changed?.Invoke();
        return true;
    }

    private void ApplyRecipeAndExit()
    {
        try
        {
            lock (sync)
            {
                ApplyRecipeForm();
                recipeInput = false;
                recipeForm = null;
            }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                recipeForm?.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "edit drink recipe")).Message);
            }
        }

        Changed?.Invoke();
    }

    private void ApplyRecipeCommand(char key)
    {
        try
        {
            lock (sync)
            {
                DrinkRecipeEditor active = recipe
                    ?? throw AppError.FailedPrecondition("drink recipe editor is missing");
                ApplyRecipeForm();
                switch (key)
                {
                    case AddIngredientKey:
                        active.AddIngredient();
                        recipeIngredientIndex = active.Ingredients.Count - 1;
                        break;
                    case AddStepKey:
                        active.AddStep();
                        recipeStepIndex = active.Steps.Count - 1;
                        break;
                    case NextIngredientKey:
                        recipeIngredientIndex = (recipeIngredientIndex + 1) % active.Ingredients.Count;
                        break;
                    case PreviousIngredientKey:
                        recipeIngredientIndex = (recipeIngredientIndex - 1 + active.Ingredients.Count)
                            % active.Ingredients.Count;
                        break;
                    case NextStepKey:
                        recipeStepIndex = (recipeStepIndex + 1) % active.Steps.Count;
                        break;
                    case RemoveIngredientKey:
                        active.RemoveIngredient(recipeIngredientIndex);
                        recipeIngredientIndex = Math.Min(recipeIngredientIndex, active.Ingredients.Count - 1);
                        break;
                    case RemoveStepKey:
                        active.RemoveStep(recipeStepIndex);
                        recipeStepIndex = Math.Min(recipeStepIndex, active.Steps.Count - 1);
                        break;
                }

                recipeForm = BuildRecipeForm();
            }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                recipeForm?.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "edit drink recipe")).Message);
            }
        }

        Changed?.Invoke();
    }

    private WorkspaceForm BuildRecipeForm()
    {
        DrinkRecipeEditor active = recipe
            ?? throw AppError.FailedPrecondition("drink recipe editor is missing");
        RecipeIngredientDraft ingredient = active.Ingredients[recipeIngredientIndex];
        string ingredientValue = active.Catalog.FirstOrDefault(value => value.Id == ingredient.IngredientId)?.Name
            ?? (ingredient.IngredientId.IsEmpty ? string.Empty : ingredient.IngredientId.Value);
        string substitutes = string.Join(',', ingredient.Substitutes.Select(id =>
            active.Catalog.FirstOrDefault(value => value.Id == id)?.Name ?? id.Value));
        return new WorkspaceForm(
        [
            new FormField("Ingredient search", ingredientValue),
            new FormField("Amount", ingredient.Amount),
            new FormField("Unit", ingredient.Unit),
            new FormField("Optional", ingredient.Optional ? "yes" : "no"),
            new FormField("Substitute searches", substitutes),
            new FormField("Step", active.Steps[recipeStepIndex]),
            new FormField("Garnish", active.Garnish),
        ]);
    }

    private void ApplyRecipeForm()
    {
        DrinkRecipeEditor active = recipe
            ?? throw AppError.FailedPrecondition("drink recipe editor is missing");
        WorkspaceForm input = recipeForm
            ?? throw AppError.FailedPrecondition("drink recipe input is missing");
        IngredientId ingredient = ResolveIngredient(active, input["Ingredient search"]);
        bool optional = ParseOptional(input["Optional"]);
        IngredientId[] substitutes = input["Substitute searches"].Split(',', StringSplitOptions.TrimEntries)
            .Where(static value => value.Length > 0)
            .Select(value => ResolveIngredient(active, value))
            .ToArray();
        active.SetIngredient(
            recipeIngredientIndex,
            ingredient,
            input["Amount"],
            input["Unit"],
            optional,
            substitutes);
        active.SetStep(recipeStepIndex, input["Step"]);
        active.SetGarnish(input["Garnish"]);
        _ = active.Build();
    }

    private static IngredientId ResolveIngredient(DrinkRecipeEditor active, string query)
    {
        string value = query.Trim();
        if (value.Length == 0)
        {
            throw AppError.Invalid("ingredient search is required");
        }

        IngredientOption[] exact = active.Catalog.Where(option =>
            string.Equals(option.Id.Value, value, StringComparison.Ordinal)
            || string.Equals(option.Name, value, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exact.Length == 1)
        {
            return exact[0].Id;
        }

        IReadOnlyList<IngredientOption> matches = active.SearchCatalog(value, int.MaxValue);
        return matches.Count switch
        {
            1 => matches[0].Id,
            0 => throw AppError.Invalid($"ingredient \"{value}\" was not found"),
            _ => throw AppError.Invalid($"ingredient search \"{value}\" is ambiguous"),
        };
    }

    private static bool ParseOptional(string value) => value.Trim().ToLowerInvariant() switch
    {
        "yes" or "true" or "y" or "1" => true,
        "no" or "false" or "n" or "0" or "" => false,
        _ => throw AppError.Invalid("optional must be yes or no"),
    };

    private string RenderRecipeInput()
    {
        string formView = recipeForm?.Render(
            $"Edit Recipe · ingredient {recipeIngredientIndex + 1}/{recipe?.Ingredients.Count ?? 0} · step {recipeStepIndex + 1}/{recipe?.Steps.Count ?? 0}",
            "[Ctrl+S] apply · [Esc] back · [Ctrl+A] add ingredient · [Ctrl+T] add step")
            ?? "Loading recipe editor...";
        return string.Join('\n', formView, "[Ctrl+J/K] ingredient · [Ctrl+U] step · [Ctrl+D/X] remove");
    }

    private void SubmitForm()
    {
        WorkspaceForm active;
        DrinksWorkspaceMode origin;
        lock (sync)
        {
            if (form is null || !form.Model.TryBeginSubmit()) { Changed?.Invoke(); return; }
            active = form;
            origin = Mode;
        }

        if (origin == DrinksWorkspaceMode.Filter)
        {
            try { ApplyFilter(active); }
            catch (Exception exception)
            {
                active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "apply drink filter")).Message);
                Changed?.Invoke();
            }

            return;
        }

        Func<CancellationToken, Task<Drink>> mutation;
        try { mutation = BuildMutation(origin, active); }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(Safe(exception, "build drink mutation")).Message);
            Changed?.Invoke();
            return;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        lock (sync)
        {
            submitOrigin = origin;
            Mode = DrinksWorkspaceMode.Submitting;
            mutationError = null;
        }

        Changed?.Invoke();
        _ = requests.Track(RunMutationAsync(mutation, active, source));
    }

    private void SubmitDelete()
    {
        Drink target;
        lock (sync)
        {
            target = detail ?? throw AppError.FailedPrecondition("drink delete has no target");
            submitOrigin = DrinksWorkspaceMode.Delete;
            Mode = DrinksWorkspaceMode.Submitting;
        }

        CancellationTokenSource source = requests.Create(CancellationToken.None);
        _ = requests.Track(RunDeleteAsync(target.Id, source));
        Changed?.Invoke();
    }

    private void ApplyFilter(WorkspaceForm active)
    {
        DrinkCategory? category = string.IsNullOrWhiteSpace(active["Category"])
            ? null : DrinkCategory.Parse(active["Category"]);
        GlassType? glass = string.IsNullOrWhiteSpace(active["Glass"])
            ? null : GlassType.Parse(active["Glass"]);
        int limit = int.Parse(active["Page size"], CultureInfo.InvariantCulture);
        active.Model.CompleteSubmit();
        lock (sync)
        {
            request = new ListDrinksRequest(
                active["Exact name"].Trim(), category, glass, active["Expression"].Trim(), default, limit).Normalize();
            history.Clear();
            next = default;
            Mode = DrinksWorkspaceMode.Browse;
            form = null;
            showFilterHelp = false;
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private Func<CancellationToken, Task<Drink>> BuildMutation(DrinksWorkspaceMode origin, WorkspaceForm active)
    {
        Recipe built = recipe?.Build() ?? throw AppError.FailedPrecondition("drink recipe editor is missing");
        return origin switch
        {
            DrinksWorkspaceMode.Create => token => operations.CreateAsync(
                new CreateDrinkRequest(
                    active["Name"],
                    DrinkCategory.Parse(active["Category"]),
                    GlassType.Parse(active["Glass"]),
                    built,
                    active["Description"]),
                active.DesiredTags(TagsField),
                token),
            DrinksWorkspaceMode.Edit when detail is not null => token => operations.UpdateAsync(
                new UpdateDrinkRequest(
                    detail.Id,
                    active["Name"],
                    DrinkCategory.Parse(active["Category"]),
                    GlassType.Parse(active["Glass"]),
                    built,
                    active["Description"],
                    detail.Revision),
                active.DesiredTags(TagsField),
                token),
            _ => throw AppError.FailedPrecondition("drink form has no target"),
        };
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<Drink>> mutation,
        WorkspaceForm active,
        CancellationTokenSource source)
    {
        try
        {
            _ = await mutation(source.Token).ConfigureAwait(false);
            active.Model.CompleteSubmit();
            CompleteMutation();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync)
            {
                if (!disposed) { Mode = submitOrigin; active.Model.FailSubmit("operation cancelled"); }
            }
        }
        catch (Exception exception)
        {
            Exception safe = Safe(exception, "mutate drink from TUI");
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

    private async Task RunDeleteAsync(DrinkId id, CancellationTokenSource source)
    {
        try
        {
            _ = await operations.DeleteAsync(id, source.Token).ConfigureAwait(false);
            CompleteMutation();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync) { if (!disposed) { Mode = DrinksWorkspaceMode.Delete; } }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed)
                {
                    Mode = DrinksWorkspaceMode.Browse;
                    mutationError = Safe(exception, "delete drink from TUI");
                }
            }

            Changed?.Invoke();
        }
    }

    private void CompleteMutation()
    {
        lock (sync)
        {
            if (disposed) { return; }
            Mode = DrinksWorkspaceMode.Browse;
            form = null;
            recipeForm = null;
            recipeInput = false;
            recipe = null;
            mutationError = null;
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void CancelForm()
    {
        lock (sync)
        {
            if (form?.Model.Mode == FormMode.Edit) { form.Model.CancelEdit(); }
            form = null;
            recipeForm = null;
            recipeInput = false;
            recipe = null;
            Mode = DrinksWorkspaceMode.Browse;
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
            $"Drinks · page {history.Count + 1} · size {request.EffectiveLimit}",
            loading ? "Loading..." : $"{table.Rows.Count} result(s)",
            string.Empty,
        ];
        int rowLimit = Math.Max(viewport.Height - 8, 1);
        foreach ((Drink drink, int index) in table.Rows.Take(rowLimit).Select((value, index) => (value, index)))
        {
            string marker = index == table.SelectedIndex ? ">" : " ";
            list.Add($"{marker} {drink.Name} · {drink.Category} · {drink.Glass}");
        }

        List<string> selected = detail is null
            ? ["Select a drink to view details"]
            : DetailLines(detail);
        return string.Join('\n',
            WorkspaceRender.TwoPane(list, selected, viewport.Width),
            string.Empty,
            BrowseHelp());
    }

    private static List<string> DetailLines(Drink drink)
    {
        List<string> lines =
        [
            drink.Name,
            $"ID: {drink.Id}",
            $"Category: {drink.Category}",
            $"Glass: {drink.Glass}",
            $"Status: {drink.Status}",
            $"Tags: {(drink.Tags.Count == 0 ? "(none)" : drink.Tags.Format())}",
            "Recipe:",
        ];
        lines.AddRange(drink.Recipe.Ingredients.Select(value =>
            $"  {value.Amount} {value.IngredientId}{(value.Optional ? " (optional)" : string.Empty)}"));
        lines.AddRange(drink.Recipe.Steps.Select((value, index) => $"  {index + 1}. {value}"));
        if (!string.IsNullOrWhiteSpace(drink.Recipe.Garnish)) { lines.Add($"Garnish: {drink.Recipe.Garnish}"); }
        if (!string.IsNullOrWhiteSpace(drink.Description)) { lines.Add(drink.Description); }
        return lines;
    }

    private string BrowseHelp()
    {
        List<string> actions = [];
        AddAction(actions, DrinkActionProjector.CreateAction, "[c] create");
        AddAction(actions, DrinkActionProjector.EditAction, "[e] edit");
        AddAction(actions, DrinkActionProjector.DeleteAction, "[d] delete");
        return string.Join('\n',
            "[j/k] select  [f] filter  [h] filter help  [[/]] page  [r] refresh",
            string.Join("  ", actions));
    }

    private void AddAction(List<string> keys, ActionId id, string label)
    {
        if (!actions.TryGetValue(id, out ActionState? state) || !state.Visible) { return; }
        keys.Add(state.Enabled ? label : $"{label} disabled: {state.DisabledReason}");
    }

    private static string RenderFilterHelp() => """
        Drink filter help · [h] close

        Fields: id, name, category, glass, status, description, tags, recipe.garnish
        Comparisons: == != < <= > >= in not in
        Logic: &&/and ||/or !/not; strings: contains, startsWith, endsWith, matches

        category == "cocktail" && name.contains("gin")
        glass in ["coupe", "martini"] && status == "active"
        tags contains "featured" || recipe.garnish.contains("lime")
        """;

    private string FormTitle() => Mode switch
    {
        DrinksWorkspaceMode.Filter => "Filter Drinks",
        DrinksWorkspaceMode.Create => "Create Drink",
        DrinksWorkspaceMode.Edit => $"Edit Drink: {detail?.Name}",
        DrinksWorkspaceMode.Submitting => "Submitting drink mutation...",
        _ => "Drinks",
    };

    private string FormFooter() => Mode == DrinksWorkspaceMode.Submitting
        ? "Submitting..."
        : "[Tab] next field · [Ctrl+R] recipe · [Ctrl+S] submit · [Esc] cancel";

    private static WorkspaceForm DrinkForm(Drink? drink) => new(
    [
        new FormField("Name", drink?.Name ?? string.Empty, ValidateName),
        new FormField("Category", drink?.Category.Value ?? DrinkCategory.Cocktail.Value, ValidateCategory),
        new FormField("Glass", drink?.Glass.Value ?? GlassType.Rocks.Value, ValidateGlass),
        new FormField("Description", drink?.Description ?? string.Empty, ValidateDescription),
        new FormField(TagsField, drink?.Tags.Format() ?? string.Empty),
    ]);

    private static string? ValidateName(string value) => string.IsNullOrWhiteSpace(value)
        ? "name is required" : value.Trim().Length > 100 ? "name must be at most 100 characters" : null;
    private static string? ValidateDescription(string value) => value.Trim().Length > 500
        ? "description must be at most 500 characters" : null;
    private static string? ValidateCategory(string value)
    {
        try { DrinkCategory.Parse(value).Validate(); return null; }
        catch (InvalidError error) { return error.UserMessage; }
    }
    private static string? ValidateOptionalCategory(string value) => string.IsNullOrWhiteSpace(value)
        ? null : ValidateCategory(value);
    private static string? ValidateGlass(string value)
    {
        try { GlassType.Parse(value).Validate(); return null; }
        catch (InvalidError error) { return error.UserMessage; }
    }
    private static string? ValidateOptionalGlass(string value) => string.IsNullOrWhiteSpace(value)
        ? null : ValidateGlass(value);
    private static string? ValidatePositiveInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? null : "page size must be greater than zero";

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        DrinksModule drinks,
        IngredientsModule ingredients,
        DrinkActionProjector projector,
        TaggedMutationCoordinator taggedMutations,
        MixologySession session,
        Actor actor) : IDrinksWorkspaceOperations
    {
        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken) =>
            drinks.ListAsync(session, request, cancellationToken);
        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
            drinks.GetAsync(session, id, cancellationToken);
        public Task<IReadOnlyList<ActionState>> ProjectAsync(Drink? selected, CancellationToken cancellationToken) =>
            projector.ProjectAsync(actor, selected, cancellationToken);

        public async Task<IReadOnlyList<Ingredient>> IngredientCatalogAsync(CancellationToken cancellationToken)
        {
            List<Ingredient> result = [];
            Cursor cursor = default;
            do
            {
                Page<Ingredient> page = await ingredients.ListAsync(
                    session,
                    new ListIngredientsRequest(Cursor: cursor),
                    cancellationToken).ConfigureAwait(false);
                result.AddRange(page.Items);
                cursor = page.Next;
            }
            while (!cursor.IsEmpty);
            return result;
        }

        public Task<Drink> CreateAsync(
            CreateDrinkRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                (active, token) => drinks.CreateAsync(active, request, token),
                tags,
                static value => value.EntityUid,
                static (value, desired) => value with { Tags = desired },
                cancellationToken);

        public Task<Drink> UpdateAsync(
            UpdateDrinkRequest request,
            TagCollection? tags,
            CancellationToken cancellationToken) => taggedMutations.RunAsync(
                session,
                (active, token) => drinks.UpdateAsync(active, request, token),
                tags,
                static value => value.EntityUid,
                static (value, desired) => value with { Tags = desired },
                cancellationToken);

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken) =>
            drinks.DeleteAsync(session, id, cancellationToken);
    }
}
