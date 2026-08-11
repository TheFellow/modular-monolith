using System.Collections.ObjectModel;
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
using Mixology.Toolkits.Desktop.Threading;
using CedarEntity = Cedar.Types.Entity;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Gui.Workspaces.Tags;

public enum TagOperation
{
    Inspect,
    Add,
    Remove,
    ShowExact,
    ShowKey,
    Summary,
}

public sealed record TagTargetType(string Type, string Label);

public sealed record TagTargetViewModel(
    KernelEntityUid Uid,
    string Name,
    string Detail,
    CedarEntity Resource)
{
    public string Display => $"{Name} · {Detail} · {Uid.Id}";
}

public interface ITagsDesktopOperations
{
    Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectTargetAsync(TagTargetViewModel target, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagTargetViewModel>> ListTargetsAsync(string entityType, CancellationToken cancellationToken);
    Task<TagCollection> InspectAsync(KernelEntityUid target, CancellationToken cancellationToken);
    Task<TagMutationResult> UpsertAsync(KernelEntityUid target, Tag value, CancellationToken cancellationToken);
    Task<TagMutationResult> RemoveAsync(KernelEntityUid target, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagReference>> ShowAsync(Tag value, bool exact, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken);
}

public sealed partial class TagsViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly ITagsDesktopOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<TagsOutcome> requests = new();
    private bool canInspect;
    private bool canTag;
    private bool canUntag;
    private bool canShow;
    private bool canSummary;
    private bool disposed;
    private Task active = Task.CompletedTask;

    public TagsViewModel(ITagsDesktopOperations operations, IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        LoadTargetsCommand = new AsyncRelayCommand(LoadTargetsAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, CanExecute);
    }

    public WorkspaceId Id => NavigationProjector.TagsWorkspace;
    public string Title => "Tags";
    public bool IsDirty => Operation is TagOperation.Add or TagOperation.Remove && Value.Length > 0;
    public ObservableCollection<TagOperation> VisibleOperations { get; } =
    [
        TagOperation.Inspect,
        TagOperation.Add,
        TagOperation.Remove,
    ];
    public IReadOnlyList<TagTargetType> TargetTypes { get; } =
    [
        new(EntityIds.DrinkType, "Drinks"),
        new(EntityIds.IngredientType, "Ingredients"),
        new(EntityIds.InventoryType, "Inventory"),
        new(EntityIds.MenuType, "Menus"),
        new(EntityIds.OrderType, "Orders"),
    ];
    public ObservableCollection<TagTargetViewModel> Targets { get; } = [];
    public ObservableCollection<string> ResultTags { get; } = [];
    public ObservableCollection<TagReference> References { get; } = [];
    public ObservableCollection<TagSummary> Summaries { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand LoadTargetsCommand { get; }
    public IAsyncRelayCommand ExecuteCommand { get; }
    public Exception? Error { get; private set; }

    [ObservableProperty]
    public partial TagOperation Operation { get; set; }

    [ObservableProperty]
    public partial TagTargetType? SelectedType { get; set; }

    [ObservableProperty]
    public partial TagTargetViewModel? SelectedTarget { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool Changed { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public static Func<IDesktopWorkspace> CreateFactory(
        TaggingModule tagging,
        TaggingActionProjector projector,
        DrinksModule drinks,
        IngredientsModule ingredients,
        InventoryModule inventory,
        MenusModule menus,
        OrdersModule orders,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(tagging);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(session);
        return () => new TagsViewModel(
            new ModuleOperations(tagging, projector, drinks, ingredients, inventory, menus, orders, session, actor),
            dispatcher);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) => RefreshAsync(cancellationToken);

    public async Task DrainAsync()
    {
        while (true)
        {
            Task snapshot = active;
            await snapshot.ConfigureAwait(false);
            if (ReferenceEquals(snapshot, active))
            {
                return;
            }
        }
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async token =>
        {
            IReadOnlyList<ActionState> actions = await operations.ProjectDiscoveryAsync(token).ConfigureAwait(false);
            return TagsOutcome.Discovery(actions);
        }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await requests.DisposeAsync().ConfigureAwait(false);
    }

    partial void OnOperationChanged(TagOperation value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnValueChanged(string value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnSelectedTypeChanged(TagTargetType? value)
    {
        Targets.Clear();
        SelectedTarget = null;
        ExecuteCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTargetChanged(TagTargetViewModel? value)
    {
        canInspect = false;
        canTag = false;
        canUntag = false;
        ExecuteCommand.NotifyCanExecuteChanged();
        if (value is not null && !disposed)
        {
            active = ProjectTargetAsync(value);
        }
    }

    private Task LoadTargetsAsync() => SelectedType is null
        ? PublishInvalidAsync("entity type is required")
        : RunAsync(async token =>
        {
            IReadOnlyList<TagTargetViewModel> targets = await operations.ListTargetsAsync(SelectedType.Type, token)
                .ConfigureAwait(false);
            IReadOnlyList<ActionState> actions = targets.Count == 0
                ? []
                : await operations.ProjectTargetAsync(targets[0], token).ConfigureAwait(false);
            return TagsOutcome.ForTargets(targets, actions);
        }, CancellationToken.None);

    private Task ProjectTargetAsync(TagTargetViewModel target) => RunAsync(async token =>
    {
        IReadOnlyList<ActionState> actions = await operations.ProjectTargetAsync(target, token).ConfigureAwait(false);
        return TagsOutcome.ForTargetActions(target.Uid, actions);
    }, CancellationToken.None);

    private async Task ExecuteAsync()
    {
        if (!CanExecute())
        {
            return;
        }

        try
        {
            Tag? parsed = Operation == TagOperation.Summary
                ? null
                : Operation is TagOperation.Remove or TagOperation.ShowKey
                    ? Tag.Create(Value)
                    : Tag.Parse(Value);
            await RunAsync(async token => Operation switch
            {
                TagOperation.Inspect => TagsOutcome.Result(
                    await operations.InspectAsync(RequireTarget(), token).ConfigureAwait(false)),
                TagOperation.Add => TagsOutcome.Result(
                    await operations.UpsertAsync(RequireTarget(), parsed!.Value, token).ConfigureAwait(false)),
                TagOperation.Remove => TagsOutcome.Result(
                    await operations.RemoveAsync(RequireTarget(), parsed!.Value.Key, token).ConfigureAwait(false)),
                TagOperation.ShowExact => TagsOutcome.Result(
                    await operations.ShowAsync(parsed!.Value, true, token).ConfigureAwait(false)),
                TagOperation.ShowKey => TagsOutcome.Result(
                    await operations.ShowAsync(parsed!.Value, false, token).ConfigureAwait(false)),
                TagOperation.Summary => TagsOutcome.Result(
                    await operations.SummaryAsync(token).ConfigureAwait(false)),
                _ => throw AppError.Invalid("tag operation is invalid"),
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            await dispatcher.InvokeAsync(() => PublishError(Safe(exception, "run desktop tag operation")))
                .ConfigureAwait(false);
        }
    }

    private bool CanExecute()
    {
        if (IsLoading)
        {
            return false;
        }

        return Operation switch
        {
            TagOperation.Inspect => SelectedTarget is not null && canInspect,
            TagOperation.Add => SelectedTarget is not null && canTag && Value.Length > 0,
            TagOperation.Remove => SelectedTarget is not null && canUntag && Value.Length > 0,
            TagOperation.ShowExact or TagOperation.ShowKey => canShow && Value.Length > 0,
            TagOperation.Summary => canSummary,
            _ => false,
        };
    }

    private KernelEntityUid RequireTarget() => SelectedTarget?.Uid
        ?? throw AppError.Invalid("tag target is required");

    private async Task RunAsync(
        Func<CancellationToken, Task<TagsOutcome>> work,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "Working with tags…";
            ExecuteCommand.NotifyCanExecuteChanged();
        }, cancellationToken).ConfigureAwait(false);
        try
        {
            LatestResult<TagsOutcome> latest = await requests.RunAsync(async token =>
            {
                try
                {
                    return await work(token).ConfigureAwait(false);
                }
                catch (Exception exception) when (AppError.IsCancellation(exception))
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return TagsOutcome.Failed(Safe(exception, "load desktop tags"));
                }
            }, cancellationToken).ConfigureAwait(false);
            if (latest.IsCurrent && latest.Value is not null)
            {
                await dispatcher.InvokeAsync(() => Publish(latest.Value), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            AppError.IsCancellation(exception) && !cancellationToken.IsCancellationRequested)
        {
            // A newer request owns publication.
        }
    }

    private void Publish(TagsOutcome outcome)
    {
        if (outcome.DiscoveryActions is not null)
        {
            canShow = Enabled(outcome.DiscoveryActions, TaggingActionProjector.ShowAction);
            canSummary = Enabled(outcome.DiscoveryActions, TaggingActionProjector.SummaryAction);
            VisibleOperations.Clear();
            VisibleOperations.Add(TagOperation.Inspect);
            VisibleOperations.Add(TagOperation.Add);
            VisibleOperations.Add(TagOperation.Remove);
            if (canShow)
            {
                VisibleOperations.Add(TagOperation.ShowExact);
                VisibleOperations.Add(TagOperation.ShowKey);
            }

            if (canSummary)
            {
                VisibleOperations.Add(TagOperation.Summary);
            }

            if (!VisibleOperations.Contains(Operation))
            {
                Operation = VisibleOperations[0];
            }
        }

        if (outcome.Targets is not null)
        {
            Targets.Clear();
            foreach (TagTargetViewModel target in outcome.Targets)
            {
                Targets.Add(target);
            }

            SelectedTarget = Targets.FirstOrDefault();
        }

        if (outcome.TargetActions is not null &&
            (outcome.Target is null || outcome.Target == SelectedTarget?.Uid))
        {
            canInspect = Enabled(outcome.TargetActions, TaggingActionProjector.InspectAction);
            canTag = Enabled(outcome.TargetActions, TaggingActionProjector.TagAction);
            canUntag = Enabled(outcome.TargetActions, TaggingActionProjector.UntagAction);
        }

        ResultTags.Clear();
        References.Clear();
        Summaries.Clear();
        if (outcome.Tags is not null)
        {
            foreach (Tag tag in outcome.Tags)
            {
                ResultTags.Add(tag.ToString());
            }
        }

        foreach (TagReference reference in outcome.References ?? [])
        {
            References.Add(reference);
        }

        foreach (TagSummary summary in outcome.Summaries ?? [])
        {
            Summaries.Add(summary);
        }

        Changed = outcome.Changed;
        if (outcome.WasMutation)
        {
            Value = string.Empty;
        }

        Error = outcome.Error;
        IsLoading = false;
        StatusMessage = outcome.Error is not null
            ? AppError.Find(outcome.Error)?.UserMessage ?? "internal error"
            : outcome.Message;
        ExecuteCommand.NotifyCanExecuteChanged();
    }

    private Task PublishInvalidAsync(string message)
    {
        PublishError(AppError.Invalid(message));
        return Task.CompletedTask;
    }

    private void PublishError(Exception exception)
    {
        Error = exception;
        IsLoading = false;
        StatusMessage = AppError.Find(exception)?.UserMessage ?? "internal error";
        ExecuteCommand.NotifyCanExecuteChanged();
    }

    private static bool Enabled(IEnumerable<ActionState> actions, ActionId id) =>
        actions.Any(state => state.Id == id && state.Visible && state.Enabled);

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed record TagsOutcome(
        IReadOnlyList<ActionState>? DiscoveryActions = null,
        IReadOnlyList<TagTargetViewModel>? Targets = null,
        KernelEntityUid? Target = null,
        IReadOnlyList<ActionState>? TargetActions = null,
        TagCollection? Tags = null,
        IReadOnlyList<TagReference>? References = null,
        IReadOnlyList<TagSummary>? Summaries = null,
        bool Changed = false,
        bool WasMutation = false,
        string Message = "Ready",
        Exception? Error = null)
    {
        public static TagsOutcome Discovery(IReadOnlyList<ActionState> actions) =>
            new(DiscoveryActions: actions, Message: "Tag operations ready");

        public static TagsOutcome ForTargets(
            IReadOnlyList<TagTargetViewModel> targets,
            IReadOnlyList<ActionState> actions) => new(
                Targets: targets,
                Target: targets.Count == 0 ? null : targets[0].Uid,
                TargetActions: actions,
                Message: $"{targets.Count} active targets");

        public static TagsOutcome ForTargetActions(
            KernelEntityUid target,
            IReadOnlyList<ActionState> actions) => new(
                Target: target,
                TargetActions: actions,
                Message: "Target actions ready");

        public static TagsOutcome Result(TagCollection tags) =>
            new(Tags: tags, Message: "Tags inspected");

        public static TagsOutcome Result(TagMutationResult mutation) => new(
            Tags: mutation.Tags,
            Changed: mutation.Changed,
            WasMutation: true,
            Message: mutation.Changed ? "Tags changed" : "Tags unchanged");

        public static TagsOutcome Result(IReadOnlyList<TagReference> references) =>
            new(References: references, Message: $"{references.Count} active references");

        public static TagsOutcome Result(IReadOnlyList<TagSummary> summaries) =>
            new(Summaries: summaries, Message: $"{summaries.Count} tag summaries");

        public static TagsOutcome Failed(Exception error) => new(Error: error, Message: string.Empty);
    }

    private sealed class ModuleOperations(
        TaggingModule tagging,
        TaggingActionProjector projector,
        DrinksModule drinks,
        IngredientsModule ingredients,
        InventoryModule inventory,
        MenusModule menus,
        OrdersModule orders,
        MixologySession session,
        Actor actor) : ITagsDesktopOperations
    {
        public Task<IReadOnlyList<ActionState>> ProjectDiscoveryAsync(CancellationToken cancellationToken) =>
            projector.ProjectDiscoveryAsync(actor, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectTargetAsync(
            TagTargetViewModel target,
            CancellationToken cancellationToken) => projector.ProjectTargetAsync(actor, target.Resource, cancellationToken);

        public async Task<IReadOnlyList<TagTargetViewModel>> ListTargetsAsync(
            string entityType,
            CancellationToken cancellationToken) => entityType switch
            {
                EntityIds.DrinkType => await CollectAsync(
                    (cursor, token) => drinks.ListAsync(session, new ListDrinksRequest(Cursor: cursor), token),
                    value => new TagTargetViewModel(value.EntityUid, value.Name, value.Category.Value, value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.IngredientType => await CollectAsync(
                    (cursor, token) => ingredients.ListAsync(session, new ListIngredientsRequest(Cursor: cursor), token),
                    value => new TagTargetViewModel(value.EntityUid, value.Name, value.Category.Value, value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.InventoryType => await CollectAsync(
                    (cursor, token) => inventory.ListAsync(session, new ListInventoryRequest(Cursor: cursor), token),
                    value => new TagTargetViewModel(
                        value.EntityUid,
                        value.Id.Value,
                        $"Ingredient {value.IngredientId}",
                        value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.MenuType => await CollectAsync(
                    (cursor, token) => menus.ListAsync(session, new ListMenusRequest(Cursor: cursor), token),
                    value => new TagTargetViewModel(value.EntityUid, value.Name, value.Status.Value, value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                EntityIds.OrderType => await CollectAsync(
                    (cursor, token) => orders.ListAsync(session, new ListOrdersRequest(Cursor: cursor), token),
                    value => new TagTargetViewModel(
                        value.EntityUid,
                        value.Id.Value,
                        $"{value.Status} · menu {value.MenuId}",
                        value.ToCedarEntity()),
                    cancellationToken).ConfigureAwait(false),
                _ => throw AppError.Invalid($"unsupported tag target type: {entityType}"),
            };

        public Task<TagCollection> InspectAsync(KernelEntityUid target, CancellationToken cancellationToken) =>
            tagging.ListAsync(session, target, cancellationToken);

        public Task<TagMutationResult> UpsertAsync(
            KernelEntityUid target,
            Tag value,
            CancellationToken cancellationToken) => tagging.UpsertAsync(session, target, value, cancellationToken);

        public Task<TagMutationResult> RemoveAsync(
            KernelEntityUid target,
            string key,
            CancellationToken cancellationToken) => tagging.RemoveAsync(session, target, key, cancellationToken);

        public Task<IReadOnlyList<TagReference>> ShowAsync(
            Tag value,
            bool exact,
            CancellationToken cancellationToken) => tagging.ShowAsync(session, value, exact, cancellationToken);

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken) =>
            tagging.SummaryAsync(session, cancellationToken);

        private static async Task<IReadOnlyList<TagTargetViewModel>> CollectAsync<T>(
            Func<Cursor, CancellationToken, Task<Page<T>>> page,
            Func<T, TagTargetViewModel> project,
            CancellationToken cancellationToken)
        {
            List<TagTargetViewModel> values = [];
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
