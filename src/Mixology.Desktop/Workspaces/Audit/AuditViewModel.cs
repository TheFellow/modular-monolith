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
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Audit.Requests;
using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Desktop.Threading;

namespace Mixology.Desktop.Workspaces.Audit;

public enum AuditScope
{
    AllActivity,
    EntityHistory,
    ActorActivity,
}

public sealed record AuditRowViewModel(AuditEntry Entry, bool CanView)
{
    public string Id => Entry.Id.Value;
    public string Started => Entry.StartedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string Completed => Entry.CompletedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string Duration => (Entry.CompletedAt - Entry.StartedAt).ToString("g", CultureInfo.CurrentCulture);
    public string Action => Entry.Action;
    public string Principal => Entry.Principal.Id;
    public string Resource => Entry.Resource is { } resource ? Format(resource) : "(none)";
    public string Outcome => Entry.Success ? "Succeeded" : "Failed";
    public string Error => string.IsNullOrWhiteSpace(Entry.Error) ? "(none)" : Entry.Error;
    public string Touches => Entry.Touches.Count == 0
        ? "(none)"
        : string.Join(Environment.NewLine, Entry.Touches.Select(Format).Order(StringComparer.Ordinal));

    private static string Format(EntityUid value) => $"{value.Type}::\"{value.Id}\"";
}

public interface IAuditDesktopOperations
{
    Task<Page<AuditEntry>> ListAsync(ListAuditEntriesRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(AuditEntry selected, CancellationToken cancellationToken);
}

public sealed partial class AuditViewModel : ObservableObject, IDesktopWorkspace
{
    private readonly IAuditDesktopOperations operations;
    private readonly IUiDispatcher dispatcher;
    private readonly LatestRequest<AuditLoadOutcome> requests = new();
    private readonly List<Cursor> history = [];
    private ListAuditEntriesRequest request = new();
    private Cursor cursor;
    private Cursor next;
    private bool disposed;

    public AuditViewModel(IAuditDesktopOperations operations, IUiDispatcher? dispatcher = null)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, CanMoveNext);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, CanMovePrevious);
    }

    public WorkspaceId Id => NavigationProjector.AuditWorkspace;
    public string Title => "Audit";
    public bool IsDirty => false;
    public IReadOnlyList<AuditScope> Scopes { get; } = Enum.GetValues<AuditScope>();
    public string FilterHelp =>
        "Fields: id, action, resource, principal, started_at, completed_at, success, error. " +
        "Dates accept ISO 8601. Example: !success && error.contains(\"conflict\")";
    public ObservableCollection<AuditRowViewModel> Rows { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ApplyFilterCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public Exception? Error { get; private set; }

    [ObservableProperty]
    public partial AuditScope Scope { get; set; }

    [ObservableProperty]
    public partial string Entity { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Principal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Action { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string From { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string To { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Expression { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageSize { get; set; } = "100";

    [ObservableProperty]
    public partial AuditRowViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    partial void OnSelectedChanged(AuditRowViewModel? value)
    {
        if (value is { CanView: false })
        {
            Selected = null;
        }
    }

    public static Func<IDesktopWorkspace> CreateFactory(
        AuditModule audit,
        AuditActionProjector projector,
        MixologySession session,
        Actor actor,
        IUiDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(session);
        return () => new AuditViewModel(new ModuleOperations(audit, projector, session, actor), dispatcher);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) =>
        LoadPageAsync(cursor, cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        LoadPageAsync(cursor, cancellationToken);

    public async Task ApplyFilterAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ListAuditEntriesRequest parsed = ParseRequest();
            request = parsed;
            cursor = default;
            next = default;
            history.Clear();
            await LoadPageAsync(default, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!AppError.IsCancellation(exception))
        {
            await dispatcher.InvokeAsync(() => PublishError(Safe(exception, "apply desktop audit filter")), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await requests.DisposeAsync().ConfigureAwait(false);
    }

    private async Task NextPageAsync()
    {
        if (!CanMoveNext())
        {
            return;
        }

        history.Add(cursor);
        cursor = next;
        await LoadPageAsync(cursor, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PreviousPageAsync()
    {
        if (!CanMovePrevious())
        {
            return;
        }

        int last = history.Count - 1;
        cursor = history[last];
        history.RemoveAt(last);
        await LoadPageAsync(cursor, CancellationToken.None).ConfigureAwait(false);
    }

    private bool CanMoveNext() => !IsLoading && !next.IsEmpty;
    private bool CanMovePrevious() => !IsLoading && history.Count > 0;

    private async Task LoadPageAsync(Cursor pageCursor, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await dispatcher.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "Loading audit activity…";
            NotifyPaging();
        }, cancellationToken).ConfigureAwait(false);
        try
        {
            ListAuditEntriesRequest snapshot = request with { Cursor = pageCursor };
            LatestResult<AuditLoadOutcome> latest = await requests.RunAsync(
                token => LoadAsync(snapshot, token), cancellationToken).ConfigureAwait(false);
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

    private async Task<AuditLoadOutcome> LoadAsync(
        ListAuditEntriesRequest snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            Page<AuditEntry> page = await operations.ListAsync(snapshot, cancellationToken).ConfigureAwait(false);
            List<AuditRowViewModel> rows = new(page.Items.Count);
            foreach (AuditEntry entry in page.Items)
            {
                IReadOnlyList<ActionState> projected = await operations.ProjectAsync(entry, cancellationToken)
                    .ConfigureAwait(false);
                bool canView = projected.Any(state =>
                    state.Id == AuditActionProjector.ViewAction && state.Visible && state.Enabled);
                rows.Add(new AuditRowViewModel(entry, canView));
            }

            return new AuditLoadOutcome(rows, page.Next, null);
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            return new AuditLoadOutcome([], default, Safe(exception, "load desktop audit activity"));
        }
    }

    private void Publish(AuditLoadOutcome outcome)
    {
        if (outcome.Error is not null)
        {
            PublishError(outcome.Error);
            return;
        }

        string? selectedId = Selected?.Id;
        Rows.Clear();
        foreach (AuditRowViewModel row in outcome.Rows)
        {
            Rows.Add(row);
        }

        Selected = Rows.FirstOrDefault(row => row.CanView && row.Id == selectedId)
            ?? Rows.FirstOrDefault(static row => row.CanView);
        next = outcome.Next;
        Error = null;
        IsLoading = false;
        StatusMessage = $"{Rows.Count.ToString(CultureInfo.CurrentCulture)} audit entries";
        NotifyPaging();
    }

    private void PublishError(Exception exception)
    {
        Error = exception;
        IsLoading = false;
        StatusMessage = AppError.Find(exception)?.UserMessage ?? "internal error";
        NotifyPaging();
    }

    private void NotifyPaging()
    {
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    private ListAuditEntriesRequest ParseRequest()
    {
        if (!int.TryParse(PageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit <= 0)
        {
            throw AppError.Invalid("page size must be greater than zero");
        }

        EntityUid parsedEntity = ParseUid(Entity);
        Actor? parsedPrincipal = string.IsNullOrWhiteSpace(Principal) ? null : Actor.Parse(Principal.Trim());
        EntityUid parsedAction = ParseUid(Action);
        if (Scope == AuditScope.EntityHistory)
        {
            if (parsedEntity.IsEmpty)
            {
                throw AppError.Invalid("entity is required for history");
            }

            parsedPrincipal = null;
            parsedAction = default;
        }
        else if (Scope == AuditScope.ActorActivity)
        {
            if (parsedPrincipal is null)
            {
                throw AppError.Invalid("principal is required for actor activity");
            }

            parsedEntity = default;
            parsedAction = default;
        }

        return new ListAuditEntriesRequest(
            parsedAction,
            parsedPrincipal,
            parsedEntity,
            ParseTime(From),
            ParseTime(To),
            Expression,
            Limit: limit).Normalize();
    }

    private static EntityUid ParseUid(string raw)
    {
        string value = raw.Trim();
        if (value.Length == 0)
        {
            return default;
        }

        if (!value.Contains("::", StringComparison.Ordinal))
        {
            return EntityIds.Parse(value);
        }

        int quoted = value.LastIndexOf("::\"", StringComparison.Ordinal);
        int separator = quoted >= 0 ? quoted : value.LastIndexOf("::", StringComparison.Ordinal);
        string type = value[..separator];
        string id = value[(separator + 2)..].Trim('"');
        return type.Length == 0 || id.Length == 0
            ? throw AppError.Invalid($"invalid entity uid: {raw}")
            : new EntityUid(type, id);
    }

    private static DateTimeOffset? ParseTime(string raw)
    {
        string value = raw.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed)
            ? parsed.ToUniversalTime()
            : throw AppError.Invalid($"invalid time \"{raw}\"");
    }

    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed record AuditLoadOutcome(
        IReadOnlyList<AuditRowViewModel> Rows,
        Cursor Next,
        Exception? Error);

    private sealed class ModuleOperations(
        AuditModule audit,
        AuditActionProjector projector,
        MixologySession session,
        Actor actor) : IAuditDesktopOperations
    {
        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken) => audit.ListAsync(session, request, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectAsync(
            AuditEntry selected,
            CancellationToken cancellationToken) => projector.ProjectAsync(actor, selected, cancellationToken);
    }
}
