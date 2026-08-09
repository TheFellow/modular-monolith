using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Requests;
using CedarEntityUid = Cedar.Types.EntityUid;

namespace Mixology.Cli;

public interface IAuditCommandSession : IAsyncDisposable
{
    Task<Page<AuditEntry>> ListAsync(
        ListAuditEntriesRequest request,
        CancellationToken cancellationToken);
}

public sealed class AuditCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<IAuditCommandSession>> createSession,
    TextWriter output,
    TextWriter error)
{
    public Func<ParseResult, CancellationToken, ValueTask<IAuditCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));

    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

    public static AuditCommandContext FromModule(
        AuditModule audit,
        Func<ParseResult, MixologySession> createSession,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(createSession);
        return new AuditCommandContext(
            (result, _) => ValueTask.FromResult<IAuditCommandSession>(
                new ModuleCommandSession(audit, createSession(result))),
            output,
            error);
    }

    private sealed class ModuleCommandSession(AuditModule audit, MixologySession session)
        : IAuditCommandSession
    {
        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken) =>
            audit.ListAsync(session, request, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class AuditCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(AuditCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command audit = new("audit", "Inspect the owner-only audit log.");
        audit.Subcommands.Add(BuildList(context));
        audit.Subcommands.Add(BuildHistory(context));
        audit.Subcommands.Add(BuildActor(context));
        return audit;
    }

    private static Command BuildList(AuditCommandContext context)
    {
        AuditOptions options = new(includeStructuredFilters: true);
        Command command = new("list", "List audit entries.");
        options.AddTo(command);
        command.SetAction((result, cancellationToken) => ExecuteListAsync(
            context,
            result,
            options,
            static (_, request) => request,
            cancellationToken));
        return command;
    }

    private static Command BuildHistory(AuditCommandContext context)
    {
        Argument<string?> entity = new("entity")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Entity UID (Type::id or Type::\"id\")",
        };
        AuditOptions options = new(includeStructuredFilters: false);
        Command command = new("history", "List audit entries for an entity.");
        command.Arguments.Add(entity);
        options.AddTo(command);
        command.SetAction((result, cancellationToken) => ExecuteListAsync(
            context,
            result,
            options,
            (parseResult, request) => request with
            {
                Entity = ParseRequiredUid(parseResult.GetValue(entity), "entity"),
            },
            cancellationToken));
        return command;
    }

    private static Command BuildActor(AuditCommandContext context)
    {
        Argument<string?> actor = new("actor")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Actor (owner, manager, sommelier, bartender, or anonymous)",
        };
        AuditOptions options = new(includeStructuredFilters: false);
        Command command = new("actor", "List audit entries for an actor.");
        command.Arguments.Add(actor);
        options.AddTo(command);
        command.SetAction((result, cancellationToken) => ExecuteListAsync(
            context,
            result,
            options,
            (parseResult, request) => request with
            {
                Principal = ParseRequiredActor(parseResult.GetValue(actor)),
            },
            cancellationToken));
        return command;
    }

    private static async Task<int> ExecuteListAsync(
        AuditCommandContext context,
        ParseResult result,
        AuditOptions options,
        Func<ParseResult, ListAuditEntriesRequest, ListAuditEntriesRequest> specialize,
        CancellationToken cancellationToken)
    {
        if (result.GetValue(options.FilterHelp))
        {
            await WriteFilterHelpAsync(context.Output).ConfigureAwait(false);
            return 0;
        }

        return await ExecuteAsync(context, result, async session =>
        {
            ListAuditEntriesRequest request = specialize(result, options.ToRequest(result));
            Page<AuditEntry> page = await session.ListAsync(request, cancellationToken).ConfigureAwait(false);
            AuditEntryView[] views = page.Items.Select(ToView).ToArray();
            if (result.GetValue(options.Json))
            {
                await WriteJsonAsync(
                    context.Output,
                    new AuditPageView(views, EmptyToNull(page.Next.Value))).ConfigureAwait(false);
                return;
            }

            await WriteTableAsync(context.Output, views).ConfigureAwait(false);
            if (!page.Next.IsEmpty)
            {
                await context.Output.WriteLineAsync($"Next cursor: {page.Next}").ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteAsync(
        AuditCommandContext context,
        ParseResult result,
        Func<IAuditCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IAuditCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static EntityUid ParseRequiredUid(string? source, string name)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw AppError.Invalid($"{name} is required");
        }

        return ParseUid(source);
    }

    private static EntityUid ParseOptionalUid(string? source) =>
        string.IsNullOrWhiteSpace(source) ? default : ParseUid(source);

    private static EntityUid ParseUid(string source)
    {
        string value = source.Trim();
        try
        {
            if (value.Contains("::\"", StringComparison.Ordinal))
            {
                CedarEntityUid parsed = CedarEntityUid.ParseCedar(value);
                return new EntityUid(parsed.Type.Value, parsed.Id.Value);
            }

            int separator = value.LastIndexOf("::", StringComparison.Ordinal);
            if (separator <= 0 || separator + 2 >= value.Length)
            {
                throw new FormatException();
            }

            string type = value[..separator];
            string id = value[(separator + 2)..].Trim('"');
            if (type.Length == 0 || id.Length == 0)
            {
                throw new FormatException();
            }

            return new EntityUid(type, id);
        }
        catch (FormatException exception)
        {
            throw AppError.Invalid($"invalid entity uid \"{source}\"", exception);
        }
    }

    private static Actor? ParseOptionalActor(string? source) =>
        string.IsNullOrWhiteSpace(source) ? null : ParseRequiredActor(source);

    private static Actor ParseRequiredActor(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw AppError.Invalid("actor is required");
        }

        string value = source.Trim();
        if (!value.Contains("::", StringComparison.Ordinal))
        {
            return Actor.Parse(value);
        }

        EntityUid uid = ParseUid(value);
        if (!string.Equals(uid.Type, CedarMappings.ActorType, StringComparison.Ordinal))
        {
            throw AppError.Invalid($"invalid actor uid \"{source}\"");
        }

        return Actor.Parse(uid.Id);
    }

    private static DateTimeOffset? ParseOptionalTime(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        string value = source.Trim();
        if (DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset timestamp))
        {
            return timestamp.ToUniversalTime();
        }

        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        throw AppError.Invalid($"invalid time \"{source}\"");
    }

    private static AuditEntryView ToView(AuditEntry entry)
    {
        TimeSpan duration = entry.CompletedAt - entry.StartedAt;
        return new AuditEntryView(
            entry.Id.Value,
            FormatTime(entry.StartedAt),
            FormatTime(entry.CompletedAt),
            duration < TimeSpan.Zero ? string.Empty : FormatDuration(duration),
            entry.Action,
            entry.Resource is { } resource ? CedarName(resource) : string.Empty,
            CedarName(new EntityUid(CedarMappings.ActorType, entry.Principal.Id)),
            entry.Success,
            entry.Touches.Count,
            entry.ErrorKind?.ToString(),
            entry.Error);
    }

    private static async Task WriteTableAsync(TextWriter output, IReadOnlyList<AuditEntryView> entries)
    {
        await output.WriteLineAsync(
            "ID\tSTARTED_AT\tCOMPLETED_AT\tDURATION\tACTION\tRESOURCE\tPRINCIPAL\tSUCCESS\tTOUCHES\tERROR")
            .ConfigureAwait(false);
        foreach (AuditEntryView entry in entries)
        {
            await output.WriteLineAsync(string.Join('\t',
                entry.Id,
                entry.StartedAt,
                entry.CompletedAt,
                entry.Duration,
                entry.Action,
                entry.Resource,
                entry.Principal,
                entry.Success,
                entry.Touches,
                entry.Error ?? string.Empty)).ConfigureAwait(false);
        }
    }

    private static Task WriteJsonAsync<T>(TextWriter output, T value) =>
        output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan value) =>
        $"{value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture)}s";

    private static string CedarName(EntityUid uid) => uid.ToCedarUid().MarshalCedar();

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static async Task WriteFilterHelpAsync(TextWriter output)
    {
        await output.WriteLineAsync("FILTER SYNTAX").ConfigureAwait(false);
        await output.WriteLineAsync("  Comparisons: ==  !=  <  <=  >  >=  in  not in").ConfigureAwait(false);
        await output.WriteLineAsync("  Logic:       && / and   || / or   ! / not   (parentheses)").ConfigureAwait(false);
        await output.WriteLineAsync("  Strings:     value.contains(\"x\"), startsWith, endsWith, matches").ConfigureAwait(false);
        await output.WriteLineAsync("  Dates:       date(\"2026-08-01T00:00:00Z\")").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("FIELDS").ConfigureAwait(false);
        await output.WriteLineAsync("  id            string    Audit entry ID").ConfigureAwait(false);
        await output.WriteLineAsync("  action        string    Operation action").ConfigureAwait(false);
        await output.WriteLineAsync("  resource      string    Primary resource").ConfigureAwait(false);
        await output.WriteLineAsync("  principal     string    Actor").ConfigureAwait(false);
        await output.WriteLineAsync("  started_at    date      Start time").ConfigureAwait(false);
        await output.WriteLineAsync("  completed_at  date      Completion time").ConfigureAwait(false);
        await output.WriteLineAsync("  success       bool      Success status").ConfigureAwait(false);
        await output.WriteLineAsync("  error         string    Safe error detail").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("EXAMPLES").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'success && action.contains(\"Ingredient\")'").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'started_at >= date(\"2026-08-01T00:00:00Z\")'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter '!success && error.contains(\"conflict\")'").ConfigureAwait(false);
    }

    private sealed class AuditOptions
    {
        public AuditOptions(bool includeStructuredFilters)
        {
            IncludeStructuredFilters = includeStructuredFilters;
            Json = new Option<bool>("--json") { Description = "Output JSON" };
            Filter = new Option<string?>("--filter") { Description = "Filter expression" };
            FilterHelp = new Option<bool>("--filter-help") { Description = "Show filter fields and examples" };
            Cursor = new Option<string?>("--cursor") { Description = "Continue after a result cursor" };
            Limit = new Option<int>("--limit") { Description = "Number of entries in a cursor page (default 100)" };
            Action = new Option<string?>("--action") { Description = "Filter by action (Type::Action::id)" };
            Entity = new Option<string?>("--entity") { Description = "Filter by entity (Type::id)" };
            Principal = new Option<string?>("--principal") { Description = "Filter by actor" };
            From = new Option<string?>("--from") { Description = "Start time (RFC3339 or YYYY-MM-DD)" };
            To = new Option<string?>("--to") { Description = "End time (RFC3339 or YYYY-MM-DD)" };
        }

        public bool IncludeStructuredFilters { get; }
        public Option<bool> Json { get; }
        public Option<string?> Filter { get; }
        public Option<bool> FilterHelp { get; }
        public Option<string?> Cursor { get; }
        public Option<int> Limit { get; }
        public Option<string?> Action { get; }
        public Option<string?> Entity { get; }
        public Option<string?> Principal { get; }
        public Option<string?> From { get; }
        public Option<string?> To { get; }

        public void AddTo(Command command)
        {
            command.Options.Add(Json);
            command.Options.Add(Filter);
            command.Options.Add(FilterHelp);
            command.Options.Add(Cursor);
            command.Options.Add(Limit);
            command.Options.Add(From);
            command.Options.Add(To);
            if (IncludeStructuredFilters)
            {
                command.Options.Add(Action);
                command.Options.Add(Entity);
                command.Options.Add(Principal);
            }
        }

        public ListAuditEntriesRequest ToRequest(ParseResult result) => new(
            IncludeStructuredFilters ? ParseOptionalUid(result.GetValue(Action)) : default,
            IncludeStructuredFilters ? ParseOptionalActor(result.GetValue(Principal)) : null,
            IncludeStructuredFilters ? ParseOptionalUid(result.GetValue(Entity)) : default,
            ParseOptionalTime(result.GetValue(From)),
            ParseOptionalTime(result.GetValue(To)),
            result.GetValue(Filter),
            result.GetValue(Cursor),
            result.GetValue(Limit));
    }

    private sealed record AuditEntryView(
        string Id,
        string StartedAt,
        string CompletedAt,
        string Duration,
        string Action,
        string Resource,
        string Principal,
        bool Success,
        int Touches,
        string? ErrorKind,
        string? Error);

    private sealed record AuditPageView(IReadOnlyList<AuditEntryView> Items, string? Next);
}
