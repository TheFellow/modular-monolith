using System.CommandLine;
using System.Text.Json;
using Mixology.Application.Authentication;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Requests;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class AuditCliTests
{
    [Fact]
    public void CommandTreeExposesListHistoryAndActor()
    {
        Harness harness = new();

        Command command = AuditCommands.Build(harness.Context);

        Assert.Equal(["list", "history", "actor"], command.Subcommands.Select(static value => value.Name));
    }

    [Fact]
    public async Task ListMapsEveryStructuredOptionAndWritesCanonicalJson()
    {
        Harness harness = new();
        AuditEntry entry = Entry();
        harness.Session.Page = new Page<AuditEntry>([entry], new Cursor(AuditEntryId.New().Value));
        string cursor = AuditEntryId.New().Value;

        int exitCode = await AuditCommands.Build(harness.Context).Parse(
        [
            "list",
            "--action", "Mixology::Ingredient::Action::create",
            "--entity", $"{entry.Resource!.Value.Type}::{entry.Resource.Value.Id}",
            "--principal", "manager",
            "--from", "2026-08-09",
            "--to", "2026-08-10T12:30:00Z",
            "--filter", "success && action.contains(\"Ingredient\")",
            "--cursor", cursor,
            "--limit", "2",
            "--json",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(harness.Error.ToString());
        ListAuditEntriesRequest request = Assert.IsType<ListAuditEntriesRequest>(harness.Session.LastRequest);
        Assert.Equal(new EntityUid("Mixology::Ingredient::Action", "create"), request.Action);
        Assert.Equal(entry.Resource, request.Entity);
        Assert.Equal(Actor.Manager, request.Principal);
        Assert.Equal(new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero), request.From);
        Assert.Equal(new DateTimeOffset(2026, 8, 10, 12, 30, 0, TimeSpan.Zero), request.To);
        Assert.Equal("success && action.contains(\"Ingredient\")", request.Filter);
        Assert.Equal(cursor, request.Cursor.Value);
        Assert.Equal(2, request.Limit);
        Assert.True(harness.Session.Disposed);

        using JsonDocument document = JsonDocument.Parse(harness.Output.ToString());
        JsonElement root = document.RootElement;
        JsonElement item = root.GetProperty("items")[0];
        Assert.Equal(entry.Id.Value, item.GetProperty("id").GetString());
        Assert.Equal("1.5s", item.GetProperty("duration").GetString());
        Assert.Equal("Mixology::Actor::\"manager\"", item.GetProperty("principal").GetString());
        Assert.Equal(2, item.GetProperty("touches").GetInt32());
        Assert.Equal("Conflict", item.GetProperty("errorKind").GetString());
        Assert.Equal(harness.Session.Page.Next.Value, root.GetProperty("next").GetString());
    }

    [Fact]
    public async Task HistoryAndActorMapScopeAndWriteHumanRows()
    {
        Harness history = new();
        AuditEntry entry = Entry();
        history.Session.Page = new Page<AuditEntry>([entry], new Cursor(AuditEntryId.New().Value));

        int historyExit = await AuditCommands.Build(history.Context).Parse(
        [
            "history",
            $"{entry.Resource!.Value.Type}::{entry.Resource.Value.Id}",
            "--from", "2026-08-09T10:00:00Z",
            "--filter", "success",
            "--limit", "5",
        ]).InvokeAsync();

        Assert.Equal(0, historyExit);
        Assert.Equal(entry.Resource, history.Session.LastRequest?.Entity);
        Assert.Equal("success", history.Session.LastRequest?.Filter);
        Assert.Contains("ID\tSTARTED_AT\tCOMPLETED_AT", history.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains(entry.Action, history.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Next cursor:", history.Output.ToString(), StringComparison.Ordinal);

        Harness actor = new();
        actor.Session.Page = new Page<AuditEntry>([entry], default);
        int actorExit = await AuditCommands.Build(actor.Context).Parse(
            ["actor", "Mixology::Actor::\"manager\"", "--to", "2026-08-10"])
            .InvokeAsync();

        Assert.Equal(0, actorExit);
        Assert.Equal(Actor.Manager, actor.Session.LastRequest?.Principal);
        Assert.Equal(new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero), actor.Session.LastRequest?.To);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("history")]
    [InlineData("actor")]
    public async Task FilterHelpDoesNotCreateASessionOrRequireScope(string command)
    {
        Harness harness = new();

        int exitCode = await AuditCommands.Build(harness.Context)
            .Parse([command, "--filter-help"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(0, harness.SessionCreations);
        Assert.Contains("FILTER SYNTAX", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("started_at", harness.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidValuesAndSessionFailuresUseTheTypedErrorAdapterAndDispose()
    {
        Harness invalid = new();

        int invalidExit = await AuditCommands.Build(invalid.Context).Parse(
            ["list", "--entity", "not-a-uid"])
            .InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, invalidExit);
        Assert.Contains("invalid entity uid", invalid.Error.ToString(), StringComparison.Ordinal);
        Assert.True(invalid.Session.Disposed);

        Harness denied = new();
        denied.Session.Exception = AppError.Permission("audit denied");
        int deniedExit = await AuditCommands.Build(denied.Context).Parse(["list"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitPermission, deniedExit);
        Assert.Equal("audit denied", denied.Error.ToString().Trim());
        Assert.True(denied.Session.Disposed);
    }

    private static AuditEntry Entry()
    {
        EntityUid resource = new(EntityIds.IngredientType, IngredientId.New().Value);
        DateTimeOffset started = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);
        return new AuditEntry(
            AuditEntryId.New(),
            "Mixology::Ingredient::Action::\"create\"",
            resource,
            Actor.Manager,
            started,
            started.AddSeconds(1.5),
            false,
            ErrorKind.Conflict,
            "duplicate ingredient",
            [resource, new EntityUid(EntityIds.DrinkType, DrinkId.New().Value)]);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Context = new AuditCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<IAuditCommandSession>(Session);
                },
                Output,
                Error);
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public AuditCommandContext Context { get; }
        public int SessionCreations { get; private set; }
    }

    private sealed class FakeSession : IAuditCommandSession
    {
        public Page<AuditEntry> Page { get; set; } = new([], default);
        public ListAuditEntriesRequest? LastRequest { get; private set; }
        public Exception? Exception { get; set; }
        public bool Disposed { get; private set; }

        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Exception is null
                ? Task.FromResult(Page)
                : Task.FromException<Page<AuditEntry>>(Exception);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
