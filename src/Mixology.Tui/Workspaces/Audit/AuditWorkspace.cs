using System.Globalization;
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
using Mixology.Toolkits.Tui;
using Mixology.Tui.Workspaces.Shared;

namespace Mixology.Tui.Workspaces.Audit;

public enum AuditScope
{
    All,
    EntityHistory,
    ActorActivity,
}

public enum AuditWorkspaceMode
{
    Browse,
    Filter,
}

public interface IAuditWorkspaceOperations
{
    Task<Page<AuditEntry>> ListAsync(ListAuditEntriesRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionState>> ProjectAsync(AuditEntry? selected, CancellationToken cancellationToken);
}

public sealed class AuditWorkspace(IAuditWorkspaceOperations operations) : ITuiWorkspace
{
    public const char SubmitKey = '\u0013';
    private readonly Lock sync = new();
    private readonly IAuditWorkspaceOperations operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly WorkspaceRequestTracker requests = new();
    private readonly TableModel<AuditEntry, AuditEntryId> table = new(
        static entry => entry.Id,
        [
            new("Started", static entry => entry.StartedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            new("Action", static entry => entry.Action),
            new("Principal", static entry => entry.Principal.Id),
            new("Success", static entry => entry.Success),
        ]);
    private readonly List<Cursor> history = [];
    private Dictionary<ActionId, ActionState> actions = [];
    private ListAuditEntriesRequest request = new();
    private AuditScope scope;
    private Cursor next;
    private CancellationTokenSource? listCancellation;
    private CancellationTokenSource? actionCancellation;
    private WorkspaceForm? form;
    private Exception? loadError;
    private Exception? actionError;
    private long generation;
    private long actionGeneration;
    private bool loading;
    private bool showFilterHelp;
    private bool disposed;

    public WorkspaceId Id => NavigationProjector.AuditWorkspace;
    public string Title => "Audit";
    public AuditWorkspaceMode Mode { get; private set; }
    public AuditScope Scope => scope;
    public InputOwnership InputOwnership => Mode == AuditWorkspaceMode.Browse
        ? InputOwnership.Browse
        : InputOwnership.Edit;
    public TuiError? Status
    {
        get
        {
            lock (sync)
            {
                Exception? error = actionError ?? loadError;
                return error is null ? null : TuiErrorAdapter.Adapt(error);
            }
        }
    }
    public IReadOnlyList<AuditEntry> Rows => table.Rows;
    public AuditEntry? Selected
    {
        get
        {
            lock (sync)
            {
                return table.TryGetSelected(out AuditEntry? selected) ? selected : null;
            }
        }
    }
    public event Action? Changed;

    public static Func<ITuiWorkspace> CreateFactory(
        AuditModule audit,
        AuditActionProjector projector,
        MixologySession session,
        Actor actor)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(session);
        return () => new AuditWorkspace(new ModuleOperations(audit, projector, session, actor));
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
        if (Mode == AuditWorkspaceMode.Filter)
        {
            if (key == '\u001b')
            {
                CancelFilter();
            }
            else if (key is SubmitKey or '\r')
            {
                SubmitFilter();
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

        switch (key)
        {
            case 'j': MoveSelection(1); return true;
            case 'k': MoveSelection(-1); return true;
            case 'f': StartFilter(); return true;
            case 'h':
            case 'H': showFilterHelp = !showFilterHelp; Changed?.Invoke(); return true;
            case ']': NextPage(); return true;
            case '[': PreviousPage(); return true;
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
                AuditWorkspaceMode.Filter => form?.Render("Query Audit", "[Tab] next field · [Ctrl+S] apply · [Esc] cancel") ?? string.Empty,
                _ when showFilterHelp => RenderFilterHelp(),
                _ => RenderBrowse(viewport),
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
            _ = ++actionGeneration;
        }

        await requests.DisposeAsync().ConfigureAwait(false);
    }

    private Task StartListAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = requests.Create(cancellationToken);
        CancellationTokenSource? previous;
        ListAuditEntriesRequest snapshot;
        long token;
        lock (sync)
        {
            previous = listCancellation;
            listCancellation = source;
            token = ++generation;
            snapshot = request;
            loading = true;
            loadError = null;
        }

        previous?.Cancel();
        Changed?.Invoke();
        return requests.Track(LoadAsync(token, snapshot, source));
    }

    private async Task LoadAsync(long token, ListAuditEntriesRequest snapshot, CancellationTokenSource source)
    {
        try
        {
            Page<AuditEntry> page = await operations.ListAsync(snapshot, source.Token).ConfigureAwait(false);
            AuditEntryId? selected;
            lock (sync)
            {
                if (disposed || token != generation)
                {
                    return;
                }

                selected = table.TryGetSelected(out AuditEntry? current) ? current?.Id : null;
                table.Replace(page.Items);
                if (selected is { } id)
                {
                    int index = table.Rows.ToList().FindIndex(entry => entry.Id == id);
                    if (index >= 0)
                    {
                        table.Select(index);
                    }
                }

                next = page.Next;
                loading = false;
            }

            await StartActionsAsync(source.Token).ConfigureAwait(false);
            Changed?.Invoke();
        }
        catch (Exception exception) when (AppError.IsCancellation(exception))
        {
            lock (sync)
            {
                if (!disposed && token == generation)
                {
                    loading = false;
                }
            }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && token == generation)
                {
                    loadError = Safe(exception, "load audit workspace");
                    loading = false;
                }
            }

            Changed?.Invoke();
        }
    }

    private Task StartActionsAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = requests.Create(cancellationToken);
        CancellationTokenSource? previous;
        AuditEntry? selected;
        long token;
        lock (sync)
        {
            previous = actionCancellation;
            actionCancellation = source;
            selected = table.TryGetSelected(out AuditEntry? current) ? current : null;
            token = ++actionGeneration;
        }

        previous?.Cancel();
        return requests.Track(SyncActionsAsync(token, selected, source.Token));
    }

    private async Task SyncActionsAsync(
        long token,
        AuditEntry? selected,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ActionState> projected = await operations.ProjectAsync(selected, cancellationToken).ConfigureAwait(false);
            lock (sync)
            {
                if (disposed || token != actionGeneration)
                {
                    return;
                }

                actions = projected.ToDictionary(static state => state.Id);
                actionError = null;
                if (!Enabled(AuditActionProjector.ListAction))
                {
                    table.Replace([]);
                }
            }
        }
        catch (Exception exception) when (AppError.IsCancellation(exception)) { throw; }
        catch (Exception exception)
        {
            lock (sync)
            {
                if (!disposed && token == actionGeneration)
                {
                    actions = [];
                    actionError = Safe(exception, "project audit actions");
                }
            }
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

            table.Select(Math.Clamp(table.SelectedIndex + delta, 0, table.Rows.Count - 1));
        }

        _ = StartActionsAsync(CancellationToken.None);
        Changed?.Invoke();
    }

    private void NextPage()
    {
        lock (sync)
        {
            if (next.IsEmpty || loading)
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
            if (history.Count == 0 || loading)
            {
                return;
            }

            Cursor cursor = history[^1];
            history.RemoveAt(history.Count - 1);
            request = request with { Cursor = cursor };
        }

        _ = StartListAsync(CancellationToken.None);
    }

    private void StartFilter()
    {
        lock (sync)
        {
            if (!Enabled(AuditActionProjector.ListAction) && actions.Count != 0)
            {
                return;
            }

            Mode = AuditWorkspaceMode.Filter;
            form = new WorkspaceForm(
            [
                new FormField("Scope", ScopeText(scope)),
                new FormField("Entity", request.Entity.IsEmpty ? string.Empty : FormatUid(request.Entity)),
                new FormField("Principal", request.Principal?.Id ?? string.Empty),
                new FormField("Action", request.Action.IsEmpty ? string.Empty : FormatUid(request.Action)),
                new FormField("From", FormatTime(request.From)),
                new FormField("To", FormatTime(request.To)),
                new FormField("Expression", request.Filter ?? string.Empty),
                new FormField("Page size", request.EffectiveLimit.ToString(CultureInfo.InvariantCulture)),
            ]);
        }

        Changed?.Invoke();
    }

    private void SubmitFilter()
    {
        WorkspaceForm active;
        lock (sync)
        {
            active = form ?? throw AppError.FailedPrecondition("audit filter is not active");
            if (!active.Model.TryBeginSubmit())
            {
                return;
            }
        }

        try
        {
            AuditScope parsedScope = ParseScope(active["Scope"]);
            EntityUid entity = ParseOptionalUid(active["Entity"]);
            Actor? principal = string.IsNullOrWhiteSpace(active["Principal"])
                ? null
                : Actor.Parse(active["Principal"]);
            EntityUid action = ParseOptionalUid(active["Action"]);
            if (parsedScope == AuditScope.EntityHistory && entity.IsEmpty)
            {
                throw AppError.Invalid("entity is required for history");
            }

            if (parsedScope == AuditScope.ActorActivity && principal is null)
            {
                throw AppError.Invalid("principal is required for actor activity");
            }

            if (parsedScope == AuditScope.EntityHistory)
            {
                principal = null;
                action = default;
            }
            else if (parsedScope == AuditScope.ActorActivity)
            {
                entity = default;
                action = default;
            }

            if (!int.TryParse(
                active["Page size"],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int limit)
                || limit <= 0)
            {
                throw AppError.Invalid("page size must be greater than zero");
            }
            request = new ListAuditEntriesRequest(
                action,
                principal,
                entity,
                ParseTime(active["From"]),
                ParseTime(active["To"]),
                active["Expression"],
                Limit: limit).Normalize();
            active.Model.CompleteSubmit();
            lock (sync)
            {
                scope = parsedScope;
                history.Clear();
                next = default;
                form = null;
                Mode = AuditWorkspaceMode.Browse;
                showFilterHelp = false;
            }

            _ = StartListAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            active.Model.FailSubmit(TuiErrorAdapter.Adapt(exception).Message);
            Changed?.Invoke();
        }
    }

    private void CancelFilter()
    {
        lock (sync)
        {
            form?.Model.CancelEdit();
            form = null;
            Mode = AuditWorkspaceMode.Browse;
        }

        Changed?.Invoke();
    }

    private string RenderBrowse(Viewport viewport)
    {
        List<string> left =
        [
            $"Audit · {ScopeText(scope)} · page {history.Count + 1}",
            loading ? "Loading..." : $"{table.Rows.Count} result(s)",
            string.Empty,
        ];
        foreach ((AuditEntry entry, int index) in table.Rows.Take(Math.Max(viewport.Height - 8, 1)).Select((value, index) => (value, index)))
        {
            string marker = index == table.SelectedIndex ? ">" : " ";
            left.Add($"{marker} {entry.StartedAt:HH:mm:ss} {entry.Action} · {entry.Principal.Id}");
        }

        List<string> right = Selected is not { } selected || !Enabled(AuditActionProjector.ViewAction)
            ? ["Select an entry to view details"]
            : Detail(selected);
        return string.Join('\n',
            WorkspaceRender.TwoPane(left, right, viewport.Width),
            string.Empty,
            "[j/k] select  [f] filter  [h] filter help  [[/]] page  [r] refresh");
    }

    private static List<string> Detail(AuditEntry entry)
    {
        List<string> lines =
        [
            "Audit Entry",
            $"ID: {entry.Id}",
            $"Action: {entry.Action}",
            $"Principal: {entry.Principal.Id}",
            $"Resource: {(entry.Resource is { } resource ? FormatUid(resource) : "(none)")}",
            $"Started: {entry.StartedAt:O}",
            $"Completed: {entry.CompletedAt:O}",
            $"Success: {entry.Success}",
        ];
        if (!string.IsNullOrWhiteSpace(entry.Error))
        {
            lines.AddRange([string.Empty, "Error", entry.Error]);
        }

        lines.AddRange([string.Empty, "Touched Entities"]);
        lines.AddRange(entry.Touches.Count == 0
            ? ["(none)"]
            : entry.Touches.Select(static uid => $"- {FormatUid(uid)}").Order(StringComparer.Ordinal));
        return lines;
    }

    private static string RenderFilterHelp() => """
        Audit filter help · [h] close

        Scopes: all activity, entity history, actor activity
        Fields: id, action, resource, principal, started_at, completed_at, success, error
        Dates: RFC3339 or YYYY-MM-DD

        success && action.contains("Ingredient")
        started_at >= date("2026-08-01T00:00:00Z")
        !success && error.contains("conflict")
        """;

    private bool Enabled(ActionId id) =>
        actions.TryGetValue(id, out ActionState? state) && state.Visible && state.Enabled;

    private static AuditScope ParseScope(string value) => value.Trim().ToLowerInvariant() switch
    {
        "all" or "all activity" => AuditScope.All,
        "entity" or "entity history" => AuditScope.EntityHistory,
        "actor" or "actor activity" => AuditScope.ActorActivity,
        _ => throw AppError.Invalid("invalid audit scope"),
    };

    private static string ScopeText(AuditScope value) => value switch
    {
        AuditScope.EntityHistory => "entity history",
        AuditScope.ActorActivity => "actor activity",
        _ => "all activity",
    };

    private static EntityUid ParseOptionalUid(string raw)
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
        if (type.Length == 0 || id.Length == 0)
        {
            throw AppError.Invalid($"invalid entity uid: {raw}");
        }

        return new EntityUid(type, id);
    }

    private static DateTimeOffset? ParseTime(string raw)
    {
        string value = raw.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw AppError.Invalid($"invalid time \"{raw}\"");
    }

    private static string FormatUid(EntityUid value) => $"{value.Type}::\"{value.Id}\"";
    private static string FormatTime(DateTimeOffset? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
    private static Exception Safe(Exception exception, string operation) =>
        AppError.Find(exception) is not null || AppError.IsCancellation(exception)
            ? exception
            : AppError.Internal(operation, exception);

    private sealed class ModuleOperations(
        AuditModule audit,
        AuditActionProjector projector,
        MixologySession session,
        Actor actor) : IAuditWorkspaceOperations
    {
        public Task<Page<AuditEntry>> ListAsync(ListAuditEntriesRequest request, CancellationToken cancellationToken) =>
            audit.ListAsync(session, request, cancellationToken);

        public Task<IReadOnlyList<ActionState>> ProjectAsync(AuditEntry? selected, CancellationToken cancellationToken) =>
            projector.ProjectAsync(actor, selected, cancellationToken);
    }
}
