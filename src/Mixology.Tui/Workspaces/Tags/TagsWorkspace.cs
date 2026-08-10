using System.Globalization;
using Cedar.Types;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Presentation.Actions;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Authorization;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Authorization;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Authorization;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Requests;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Models;
using Mixology.Modules.Tagging.Presentation;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;
using CedarEntity = Cedar.Types.Entity;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Tui.Workspaces.Tags;

public enum TagOperation
{
    Inspect,
    Add,
    Remove,
    ShowExact,
    ShowKey,
    Summary,
}

public enum TagsWorkspaceMode
{
    Operations,
    PickingType,
    PickingEntity,
    EnteringValue,
    Loading,
    Results,
}

public sealed record TagTargetChoice(KernelEntityUid Uid, string Name, string Description, CedarEntity Resource);

public sealed record TagWorkspaceResult(
    TagOperation Operation,
    KernelEntityUid Target = default,
    TagCollection? Tags = null,
    bool Changed = false,
    IReadOnlyList<TagReference>? References = null,
    IReadOnlyList<TagSummary>? Summaries = null);

public interface ITagsWorkspaceOperations
{
    Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectTargetAsync(TagTargetChoice target, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagTargetChoice>> ListTargetsAsync(string entityType, CancellationToken cancellationToken);
    Task<TagCollection> InspectAsync(KernelEntityUid target, CancellationToken cancellationToken);
    Task<TagMutationResult> UpsertAsync(KernelEntityUid target, Tag value, CancellationToken cancellationToken);
    Task<TagMutationResult> RemoveAsync(KernelEntityUid target, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagReference>> ShowAsync(Tag value, bool exact, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken);
}

public sealed class TagsWorkspace(ITagsWorkspaceOperations operations) : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    private static readonly (TagOperation Operation, string Title, string Description)[] AllOperations =
    [
        (TagOperation.Inspect, "Inspect entity tags", "Choose an entity and view its tags"),
        (TagOperation.Add, "Add or replace a tag", "Choose an entity, then enter key or key=value"),
        (TagOperation.Remove, "Remove a tag", "Choose an entity, then enter the key"),
        (TagOperation.ShowExact, "Show exact tag", "List active entities carrying key or key=value"),
        (TagOperation.ShowKey, "Show all values for key", "List active entities carrying any value for a key"),
        (TagOperation.Summary, "Tag usage summary", "Count active tag usage by entity type"),
    ];
    private static readonly (string Type, string Label)[] TargetTypes =
    [
        (EntityIds.DrinkType, "Drinks"),
        (EntityIds.IngredientType, "Ingredients"),
        (EntityIds.InventoryType, "Inventory"),
        (EntityIds.MenuType, "Menus"),
        (EntityIds.OrderType, "Orders"),
    ];

    private readonly Lock sync = new();
    private readonly ITagsWorkspaceOperations operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly WorkspaceRequestTracker requests = new();
    private (TagOperation Operation, string Title, string Description)[] visibleOperations = [];
    private IReadOnlyList<TagTargetChoice> targets = [];
    private Dictionary<ActionId, ActionState> targetActions = [];
    private CancellationTokenSource? activeCancellation;
    private WorkspaceForm? form;
    private TagWorkspaceResult? result;
    private Exception? error;
    private TagTargetChoice? target;
    private string? entityType;
    private long generation;
    private int operationIndex;
    private int typeIndex;
    private int targetIndex;
    private bool disposed;

    public WorkspaceId Id => NavigationProjector.TagsWorkspace;
    public string Title => "Tags";
    public TagsWorkspaceMode Mode { get; private set; }
    public TagOperation? Operation { get; private set; }
    public TagWorkspaceResult? Result => result;
    public IReadOnlyList<TagTargetChoice> Targets => targets;
    public InputOwnership InputOwnership => Mode == TagsWorkspaceMode.Operations
        ? InputOwnership.Browse
        : Mode == TagsWorkspaceMode.EnteringValue
            ? InputOwnership.Edit
            : new InputOwnership(false, true);
    public TuiError? Status
    {
        get
        {
            lock (sync)
            {
                return error is null ? null : TuiErrorAdapter.Adapt(error);
            }
        }
    }
    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        TaggingModule tagging,
        TaggingActionProjector projector,
        DrinksModule drinks,
        IngredientsModule ingredients,
        InventoryModule inventory,
        MenusModule menus,
        OrdersModule orders,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(tagging);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(drinks);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(session);
        return () => new TagsWorkspace(new ModuleOperations(
            tagging, projector, drinks, ingredients, inventory, menus, orders, session, actor));
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) => RefreshAsync(cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Mode != TagsWorkspaceMode.Operations)
        {
            return Task.CompletedTask;
        }

        return StartAsync(ProjectOperationsAsync, cancellationToken);
    }

    public Task DrainAsync() => requests.DrainAsync();

    public void SetValue(string value)
    {
        lock (sync)
        {
            form?.Set("Tag / key", value);
        }

        Changed?.Invoke();
    }

    public bool Handle(char key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (key == '\u001b' && Mode != TagsWorkspaceMode.Operations)
        {
            Back();
            return true;
        }

        if (Mode == TagsWorkspaceMode.EnteringValue)
        {
            if (key is SubmitKey or '\r')
            {
                SubmitValue();
            }
            else
            {
                lock (sync)
                {
                    _ = form?.Handle(key);
                }

                Changed?.Invoke();
            }

            return true;
        }

        switch (Mode)
        {
            case TagsWorkspaceMode.Operations:
                if (key == 'j')
                {
                    Move(ref operationIndex, 1, visibleOperations.Length);
                }
                else if (key == 'k')
                {
                    Move(ref operationIndex, -1, visibleOperations.Length);
                }
                else if (key is '\r' or ' ')
                {
                    SelectOperation();
                }
                else if (key == 'r')
                {
                    _ = RefreshAsync();
                }
                else
                {
                    return false;
                }

                return true;
            case TagsWorkspaceMode.PickingType:
                if (key == 'j')
                {
                    Move(ref typeIndex, 1, TargetTypes.Length);
                }
                else if (key == 'k')
                {
                    Move(ref typeIndex, -1, TargetTypes.Length);
                }
                else if (key is '\r' or ' ')
                {
                    SelectType();
                }
                else
                {
                    return false;
                }

                return true;
            case TagsWorkspaceMode.PickingEntity:
                if (key == 'j')
                {
                    Move(ref targetIndex, 1, targets.Count);
                }
                else if (key == 'k')
                {
                    Move(ref targetIndex, -1, targets.Count);
                }
                else if (key is '\r' or ' ')
                {
                    SelectTarget();
                }
                else
                {
                    return false;
                }

                return true;
            case TagsWorkspaceMode.Loading:
            case TagsWorkspaceMode.Results:
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
                TagsWorkspaceMode.Operations => RenderOperations(),
                TagsWorkspaceMode.PickingType => RenderTypes(),
                TagsWorkspaceMode.PickingEntity => RenderTargets(),
                TagsWorkspaceMode.EnteringValue => form?.Render(OperationTitle(), "[Ctrl+S] submit · [Esc] cancel") ?? string.Empty,
                TagsWorkspaceMode.Loading => "Working with tags...\n\n[Esc] cancel",
                TagsWorkspaceMode.Results => RenderResult(),
                _ => string.Empty,
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
            _ = ++generation;
        }

        await requests.DisposeAsync().ConfigureAwait(false);
    }

    private Task StartAsync(Func<long, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        CancellationTokenSource source = requests.Create(cancellationToken);
        CancellationTokenSource? previous;
        long token;
        lock (sync)
        {
            previous = activeCancellation;
            activeCancellation = source;
            token = ++generation;
            error = null;
        }

        previous?.Cancel();
        Changed?.Invoke();
        return requests.Track(RunSafeAsync(token, source, work));
    }

    private async Task RunSafeAsync(
        long token,
        CancellationTokenSource source,
        Func<long, CancellationToken, Task> work)
    {
        try
        {
            await work(token, source.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && token == generation)
                {
                    error = Safe(exception, "run tags workspace operation");
                    Mode = TagsWorkspaceMode.Results;
                }
            }

            Changed?.Invoke();
        }
    }

    private async Task ProjectOperationsAsync(long token, CancellationToken cancellationToken)
    {
        IReadOnlyList<ActionState> states = await operations.ProjectDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        bool show = Enabled(states, TaggingActionProjector.ShowAction);
        bool summary = Enabled(states, TaggingActionProjector.SummaryAction);
        var visible = AllOperations.Where(item => item.Operation switch
        {
            TagOperation.ShowExact or TagOperation.ShowKey => show,
            TagOperation.Summary => summary,
            _ => true,
        }).ToArray();
        lock (sync)
        {
            if (disposed || token != generation)
            {
                return;
            }

            visibleOperations = visible;
            operationIndex = Math.Clamp(operationIndex, 0, Math.Max(visible.Length - 1, 0));
        }

        Changed?.Invoke();
    }

    private void SelectOperation()
    {
        TagOperation selected;
        lock (sync)
        {
            if (visibleOperations.Length == 0)
            {
                return;
            }

            selected = visibleOperations[operationIndex].Operation;
            Operation = selected;
            result = null;
            error = null;
            target = null;
            entityType = null;
            if (selected is TagOperation.Inspect or TagOperation.Add or TagOperation.Remove)
            {
                Mode = TagsWorkspaceMode.PickingType;
                typeIndex = 0;
            }
            else if (selected is TagOperation.ShowExact or TagOperation.ShowKey)
            {
                BeginValueForm();
            }
            else
            {
                Mode = TagsWorkspaceMode.Loading;
            }
        }

        Changed?.Invoke();
        if (selected == TagOperation.Summary)
        {
            _ = StartAsync(RunOperationAsync, CancellationToken.None);
        }
    }

    private void SelectType()
    {
        string selected;
        lock (sync)
        {
            selected = TargetTypes[typeIndex].Type;
            entityType = selected;
            Mode = TagsWorkspaceMode.Loading;
        }

        _ = StartAsync(async (token, cancellationToken) =>
        {
            IReadOnlyList<TagTargetChoice> loaded = await operations.ListTargetsAsync(selected, cancellationToken).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || token != generation)
                {
                    return;
                }

                targets = loaded;
                targetIndex = 0;
                Mode = TagsWorkspaceMode.PickingEntity;
            }

            Changed?.Invoke();
        }, CancellationToken.None);
    }

    private void SelectTarget()
    {
        TagTargetChoice selected;
        lock (sync)
        {
            if (targets.Count == 0)
            {
                return;
            }

            selected = targets[targetIndex];
            target = selected;
            Mode = TagsWorkspaceMode.Loading;
        }

        _ = StartAsync(async (token, cancellationToken) =>
        {
            IReadOnlyList<ActionState> states = await operations.ProjectTargetAsync(selected, cancellationToken).ConfigureAwait(false);
            ActionId required = Operation switch
            {
                TagOperation.Inspect => TaggingActionProjector.InspectAction,
                TagOperation.Add => TaggingActionProjector.TagAction,
                TagOperation.Remove => TaggingActionProjector.UntagAction,
                _ => throw AppError.FailedPrecondition("tag target operation is invalid"),
            };
            if (!Enabled(states, required))
            {
                throw AppError.Permission("tag operation is not permitted");
            }

            lock (sync)
            {
                if (disposed || token != generation)
                {
                    return;
                }

                targetActions = states.ToDictionary(static state => state.Id);
                if (Operation == TagOperation.Inspect)
                {
                    Mode = TagsWorkspaceMode.Loading;
                }
                else
                {
                    BeginValueForm();
                }
            }

            Changed?.Invoke();
            if (Operation == TagOperation.Inspect)
            {
                await RunOperationAsync(token, cancellationToken).ConfigureAwait(false);
            }
        }, CancellationToken.None);
    }

    private void BeginValueForm()
    {
        form = new WorkspaceForm([new FormField("Tag / key")]);
        Mode = TagsWorkspaceMode.EnteringValue;
    }

    private void SubmitValue()
    {
        WorkspaceForm active;
        lock (sync)
        {
            active = form ?? throw AppError.FailedPrecondition("tag value form is not active");
            if (!active.Model.TryBeginSubmit())
            {
                return;
            }
        }

        try
        {
            string raw = active["Tag / key"].Trim();
            Tag parsed = Operation is TagOperation.Remove or TagOperation.ShowKey
                ? Tag.Create(raw)
                : Tag.Parse(raw);
            active.Model.CompleteSubmit();
            lock (sync)
            {
                form = null;
                submitted = parsed;
                Mode = TagsWorkspaceMode.Loading;
                error = null;
            }

            _ = StartAsync(RunOperationAsync, CancellationToken.None);
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(exception).Message);
            Changed?.Invoke();
        }
    }

    private Tag submitted;

    private async Task RunOperationAsync(long token, CancellationToken cancellationToken)
    {
        TagOperation operation = Operation ?? throw AppError.FailedPrecondition("tag operation is not selected");
        KernelEntityUid uid = target?.Uid ?? default;
        TagWorkspaceResult completed = operation switch
        {
            TagOperation.Inspect => new(operation, uid,
                await operations.InspectAsync(uid, cancellationToken).ConfigureAwait(false)),
            TagOperation.Add => FromMutation(operation,
                await operations.UpsertAsync(uid, submitted, cancellationToken).ConfigureAwait(false)),
            TagOperation.Remove => FromMutation(operation,
                await operations.RemoveAsync(uid, submitted.Key, cancellationToken).ConfigureAwait(false)),
            TagOperation.ShowExact => new(operation, References:
                await operations.ShowAsync(submitted, true, cancellationToken).ConfigureAwait(false)),
            TagOperation.ShowKey => new(operation, References:
                await operations.ShowAsync(submitted, false, cancellationToken).ConfigureAwait(false)),
            TagOperation.Summary => new(operation, Summaries:
                await operations.SummaryAsync(cancellationToken).ConfigureAwait(false)),
            _ => throw AppError.FailedPrecondition("tag operation is invalid"),
        };
        lock (sync)
        {
            if (disposed || token != generation)
            {
                return;
            }

            result = completed;
            error = null;
            Mode = TagsWorkspaceMode.Results;
        }

        Changed?.Invoke();
    }

    private void Back()
    {
        activeCancellation?.Cancel();
        lock (sync)
        {
            _ = ++generation;
            switch (Mode)
            {
                case TagsWorkspaceMode.PickingEntity:
                    Mode = TagsWorkspaceMode.PickingType;
                    break;
                case TagsWorkspaceMode.EnteringValue when target is not null:
                    Mode = TagsWorkspaceMode.PickingEntity;
                    break;
                default:
                    Mode = TagsWorkspaceMode.Operations;
                    Operation = null;
                    target = null;
                    entityType = null;
                    break;
            }

            form = null;
            result = null;
            error = null;
        }

        Changed?.Invoke();
    }

    private void Move(ref int index, int delta, int count)
    {
        lock (sync)
        {
            if (count == 0)
            {
                return;
            }

            index = Math.Clamp(index + delta, 0, count - 1);
        }

        Changed?.Invoke();
    }

    private string RenderOperations()
    {
        List<string> lines = ["Tags", string.Empty];
        for (int index = 0; index < visibleOperations.Length; index++)
        {
            var item = visibleOperations[index];
            lines.Add($"{(index == operationIndex ? '>' : ' ')} {item.Title}");
            lines.Add($"    {item.Description}");
        }

        if (visibleOperations.Length == 0)
        {
            lines.Add("No authorized tag operations");
        }

        lines.AddRange([string.Empty, "[j/k] select · [Enter] open · [r] refresh"]);
        return string.Join('\n', lines);
    }

    private string RenderTypes()
    {
        List<string> lines = ["Select entity type", string.Empty];
        for (int index = 0; index < TargetTypes.Length; index++)
        {
            lines.Add($"{(index == typeIndex ? '>' : ' ')} {TargetTypes[index].Label}");
        }

        lines.AddRange([string.Empty, "[j/k] select · [Enter] open · [Esc] back"]);
        return string.Join('\n', lines);
    }

    private string RenderTargets()
    {
        List<string> lines = [$"Select {TargetTypes.Single(value => value.Type == entityType).Label.ToLowerInvariant()}", string.Empty];
        for (int index = 0; index < targets.Count; index++)
        {
            TagTargetChoice item = targets[index];
            lines.Add($"{(index == targetIndex ? '>' : ' ')} {item.Name}");
            lines.Add($"    {item.Description} · {item.Uid.Id}");
        }

        if (targets.Count == 0)
        {
            lines.Add("No active authorized entities");
        }

        lines.AddRange([string.Empty, "[j/k] select · [Enter] open · [Esc] back"]);
        return string.Join('\n', lines);
    }

    private string RenderResult()
    {
        List<string> lines = [OperationTitle(), string.Empty];
        if (error is not null)
        {
            lines.Add($"Error: {TuiErrorAdapter.Adapt(error).Message}");
        }
        else if (result is { } value)
        {
            switch (value.Operation)
            {
                case TagOperation.Inspect:
                case TagOperation.Add:
                case TagOperation.Remove:
                    lines.Add("ENTITY                          TAGS                         RESULT");
                    lines.Add($"{value.Target.Id}  {value.Tags?.Format() ?? "(none)"}  {ResultState(value)}");
                    break;
                case TagOperation.ShowExact:
                case TagOperation.ShowKey:
                    lines.Add("ENTITY TYPE  ENTITY NAME  ENTITY ID  TAG");
                    lines.AddRange((value.References ?? []).Select(reference =>
                        $"{reference.EntityType}  {reference.EntityName}  {reference.EntityId}  {reference.Tag}"));
                    if (value.References?.Count == 0)
                    {
                        lines.Add("(none)");
                    }

                    break;
                case TagOperation.Summary:
                    lines.Add("TAG  TOTAL  DRINKS  INGREDIENTS  INVENTORY  MENUS  ORDERS");
                    lines.AddRange((value.Summaries ?? []).Select(summary => string.Create(
                        CultureInfo.InvariantCulture,
                        $"{summary.Tag}  {summary.Total}  {summary.Drinks}  {summary.Ingredients}  {summary.Inventory}  {summary.Menus}  {summary.Orders}")));
                    if (value.Summaries?.Count == 0)
                    {
                        lines.Add("(none)");
                    }

                    break;
            }
        }

        lines.AddRange([string.Empty, "[Esc] back"]);
        return string.Join('\n', lines);
    }

    private string OperationTitle() => Operation is { } selected
        ? AllOperations.Single(item => item.Operation == selected).Title
        : "Tags";

    private static string ResultState(TagWorkspaceResult result) => result.Operation == TagOperation.Inspect
        ? "inspected"
        : result.Changed ? "changed" : "unchanged";

    private static TagWorkspaceResult FromMutation(TagOperation operation, TagMutationResult mutation) =>
        new(operation, mutation.Target, mutation.Tags, mutation.Changed);

    private static bool Enabled(IEnumerable<ActionState> states, ActionId id) =>
        states.Any(state => state.Id == id && state.Visible && state.Enabled);

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        TaggingModule tagging,
        TaggingActionProjector projector,
        DrinksModule drinks,
        IngredientsModule ingredients,
        InventoryModule inventory,
        MenusModule menus,
        OrdersModule orders,
        MixologySession session,
        Actor actor) : ITagsWorkspaceOperations
    {
        public Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken) =>
            projector.ProjectDiscoveryAsync(actor, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectTargetAsync(TagTargetChoice target, CancellationToken cancellationToken) =>
            projector.ProjectTargetAsync(actor, target.Resource, cancellationToken);

        public async Task<IReadOnlyList<TagTargetChoice>> ListTargetsAsync(
            string entityType,
            CancellationToken cancellationToken) => entityType switch
            {
                EntityIds.DrinkType => await CollectAsync(
                    (cursor, token) => drinks.ListAsync(session, new ListDrinksRequest(Cursor: cursor), token),
                    value => new TagTargetChoice(value.EntityUid, value.Name, value.Category.Value, value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.IngredientType => await CollectAsync(
                    (cursor, token) => ingredients.ListAsync(session, new ListIngredientsRequest(Cursor: cursor), token),
                    value => new TagTargetChoice(value.EntityUid, value.Name, value.Category.Value, value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.InventoryType => await CollectAsync(
                    (cursor, token) => inventory.ListAsync(session, new ListInventoryRequest(Cursor: cursor), token),
                    value => new TagTargetChoice(value.EntityUid, value.Id.Value, $"Ingredient {value.IngredientId}", value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.MenuType => await CollectAsync(
                    (cursor, token) => menus.ListAsync(session, new ListMenusRequest(Cursor: cursor), token),
                    value => new TagTargetChoice(value.EntityUid, value.Name, value.Status.Value, value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.OrderType => await CollectAsync(
                    (cursor, token) => orders.ListAsync(session, new ListOrdersRequest(Cursor: cursor), token),
                    value => new TagTargetChoice(value.EntityUid, value.Id.Value, $"{value.Status} · menu {value.MenuId}", value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                _ => throw AppError.Invalid($"unsupported tag target type: {entityType}"),
            };

        public Task<TagCollection> InspectAsync(KernelEntityUid target, CancellationToken cancellationToken) =>
            tagging.ListAsync(session, target, cancellationToken);

        public Task<TagMutationResult> UpsertAsync(KernelEntityUid target, Tag value, CancellationToken cancellationToken) =>
            tagging.UpsertAsync(session, target, value, cancellationToken);

        public Task<TagMutationResult> RemoveAsync(KernelEntityUid target, string key, CancellationToken cancellationToken) =>
            tagging.RemoveAsync(session, target, key, cancellationToken);

        public Task<IReadOnlyList<TagReference>> ShowAsync(Tag value, bool exact, CancellationToken cancellationToken) =>
            tagging.ShowAsync(session, value, exact, cancellationToken);

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken) =>
            tagging.SummaryAsync(session, cancellationToken);

        private static async Task<IReadOnlyList<TagTargetChoice>> CollectAsync<T>(
            Func<Cursor, CancellationToken, Task<Page<T>>> page,
            Func<T, TagTargetChoice> project,
            CancellationToken cancellationToken)
        {
            List<TagTargetChoice> values = [];
            Cursor cursor = default;
            do
            {
                Page<T> next = await page(cursor, cancellationToken).ConfigureAwait(false);
                values.AddRange(next.Items.Select(project));
                cursor = next.Next;
            }
            while (!cursor.IsEmpty);
            return values;
        }
    }
}
