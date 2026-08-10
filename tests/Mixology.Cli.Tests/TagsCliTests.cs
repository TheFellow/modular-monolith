using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Tagging.Models;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class TagsCliTests
{
    [Fact]
    public void CommandTreeExposesTheGoTagVerbs()
    {
        Harness harness = new();

        Command command = TagsCommands.Build(harness.Context);

        Assert.Equal(
            ["show", "summary", "add", "remove", "list"],
            command.Subcommands.Select(static value => value.Name));
    }

    [Fact]
    public async Task ShowSupportsExactTagAndKeyOnlyDiscovery()
    {
        Harness exact = new();
        exact.Session.References =
        [
            new TagReference("Drink", "Gimlet", DrinkId.New().Value, "region=west"),
        ];

        int exactExit = await TagsCommands.Build(exact.Context).Parse(["show", "region=west"]).InvokeAsync();

        Assert.Equal(0, exactExit);
        Assert.Equal(new Tag("region", "west"), exact.Session.LastShowTag);
        Assert.True(exact.Session.LastShowExact);
        Assert.Contains("ENTITY_TYPE\tENTITY_NAME\tENTITY_ID\tTAG", exact.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Drink\tGimlet", exact.Output.ToString(), StringComparison.Ordinal);

        Harness keyOnly = new();
        keyOnly.Session.References =
        [
            new TagReference("Menu", "Dinner", MenuId.New().Value, "region=east"),
        ];
        int keyExit = await TagsCommands.Build(keyOnly.Context).Parse(
            ["show", "--key", "region", "--json"]).InvokeAsync();

        Assert.Equal(0, keyExit);
        Assert.Equal(new Tag("region"), keyOnly.Session.LastShowTag);
        Assert.False(keyOnly.Session.LastShowExact);
        using JsonDocument document = JsonDocument.Parse(keyOnly.Output.ToString());
        JsonElement item = document.RootElement[0];
        Assert.Equal("Menu", item.GetProperty("entityType").GetString());
        Assert.Equal("Dinner", item.GetProperty("entityName").GetString());
        Assert.Equal("region=east", item.GetProperty("tag").GetString());
    }

    [Theory]
    [InlineData("show")]
    [InlineData("show region=west --key region")]
    public async Task ShowRequiresExactlyOneDiscoveryShape(string commandLine)
    {
        Harness harness = new();

        int exitCode = await TagsCommands.Build(harness.Context).Parse(commandLine).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, exitCode);
        Assert.Contains(
            commandLine == "show" ? "tag argument or --key is required" : "cannot be used together",
            harness.Error.ToString(),
            StringComparison.Ordinal);
        Assert.True(harness.Session.Disposed);
    }

    [Fact]
    public async Task SummaryWritesStableHumanColumnsAndCanonicalJson()
    {
        TagSummary summary = new("featured", 5, 1, 1, 1, 1, 1);
        Harness human = new();
        human.Session.Summaries = [summary];

        int humanExit = await TagsCommands.Build(human.Context).Parse(["summary"]).InvokeAsync();

        Assert.Equal(0, humanExit);
        Assert.Contains(
            "TAG\tTOTAL\tDRINKS\tINGREDIENTS\tINVENTORY\tMENUS\tORDERS",
            human.Output.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("featured\t5\t1\t1\t1\t1\t1", human.Output.ToString(), StringComparison.Ordinal);

        Harness json = new();
        json.Session.Summaries = [summary];
        int jsonExit = await TagsCommands.Build(json.Context).Parse(["summary", "--json"]).InvokeAsync();

        Assert.Equal(0, jsonExit);
        using JsonDocument document = JsonDocument.Parse(json.Output.ToString());
        Assert.Equal("featured", document.RootElement[0].GetProperty("tag").GetString());
        Assert.Equal(5, document.RootElement[0].GetProperty("total").GetInt32());
        Assert.Equal(1, document.RootElement[0].GetProperty("orders").GetInt32());
    }

    [Theory]
    [MemberData(nameof(TargetIds))]
    public async Task AddParsesEveryPolymorphicTargetPrefix(string id, string entityType)
    {
        Harness harness = new();
        harness.Session.Mutation = new TagMutationResult(
            new EntityUid(entityType, id),
            new TagCollection([new Tag("featured"), new Tag("region", "west")]),
            true);

        int exitCode = await TagsCommands.Build(harness.Context).Parse(
            ["add", id, "region=west"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(new EntityUid(entityType, id), harness.Session.LastTarget);
        Assert.Equal(new Tag("region", "west"), harness.Session.LastMutationTag);
        Assert.Equal($"{id}: featured,region=west (changed)", harness.Output.ToString().Trim());
        Assert.True(harness.Session.Disposed);
    }

    public static TheoryData<string, string> TargetIds => new()
    {
        { DrinkId.New().Value, EntityIds.DrinkType },
        { IngredientId.New().Value, EntityIds.IngredientType },
        { InventoryId.New().Value, EntityIds.InventoryType },
        { MenuId.New().Value, EntityIds.MenuType },
        { OrderId.New().Value, EntityIds.OrderType },
    };

    [Fact]
    public async Task AddJsonAndRemoveHumanExposeChangedAndCanonicalState()
    {
        Harness add = new();
        DrinkId id = DrinkId.New();
        add.Session.Mutation = new TagMutationResult(
            id.EntityUid,
            new TagCollection([new Tag("featured"), new Tag("region", "east")]),
            false);

        int addExit = await TagsCommands.Build(add.Context).Parse(
            ["add", "--json", id.Value, "region=east"]).InvokeAsync();

        Assert.Equal(0, addExit);
        using JsonDocument document = JsonDocument.Parse(add.Output.ToString());
        Assert.Equal(id.Value, document.RootElement.GetProperty("entityId").GetString());
        Assert.False(document.RootElement.GetProperty("changed").GetBoolean());
        Assert.Equal(
            ["featured", "region=east"],
            document.RootElement.GetProperty("tags").EnumerateArray().Select(static value => value.GetString()));

        Harness remove = new();
        remove.Session.Mutation = new TagMutationResult(id.EntityUid, TagCollection.Empty, true);
        int removeExit = await TagsCommands.Build(remove.Context).Parse(
            ["remove", id.Value, "region"]).InvokeAsync();

        Assert.Equal(0, removeExit);
        Assert.Equal("region", remove.Session.LastRemoveKey);
        Assert.Equal($"{id}: (none) (changed)", remove.Output.ToString().Trim());
    }

    [Fact]
    public async Task ListUsesCanonicalHumanAndJsonShapesWithoutChangedField()
    {
        MenuId id = MenuId.New();
        Harness human = new();
        human.Session.Tags = new TagCollection([new Tag("z"), new Tag("a", "1")]);

        int humanExit = await TagsCommands.Build(human.Context).Parse(["list", id.Value]).InvokeAsync();

        Assert.Equal(0, humanExit);
        Assert.Equal(id.EntityUid, human.Session.LastTarget);
        Assert.Equal($"{id}: a=1,z", human.Output.ToString().Trim());

        Harness json = new();
        json.Session.Tags = human.Session.Tags;
        int jsonExit = await TagsCommands.Build(json.Context).Parse(
            ["list", "--json", id.Value]).InvokeAsync();

        Assert.Equal(0, jsonExit);
        using JsonDocument document = JsonDocument.Parse(json.Output.ToString());
        Assert.Equal(id.Value, document.RootElement.GetProperty("entityId").GetString());
        Assert.False(document.RootElement.TryGetProperty("changed", out _));
        Assert.Equal(
            ["a=1", "z"],
            document.RootElement.GetProperty("tags").EnumerateArray().Select(static value => value.GetString()));
    }

    [Theory]
    [InlineData("list", "wat-not-supported")]
    [InlineData("list", "drk-not-a-ksuid")]
    [InlineData("add", "aud-not-a-ksuid")]
    public async Task InvalidAndUnsupportedIdsUseTheTypedErrorAdapter(string verb, string id)
    {
        Harness harness = new();
        string[] arguments = verb == "add" ? [verb, id, "featured"] : [verb, id];

        int exitCode = await TagsCommands.Build(harness.Context).Parse(arguments).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, exitCode);
        Assert.Contains("invalid entity-id", harness.Error.ToString(), StringComparison.Ordinal);
        Assert.True(harness.Session.Disposed);
    }

    [Fact]
    public async Task SessionFailuresUseTypedErrorsAndAlwaysDispose()
    {
        Harness harness = new();
        harness.Session.Exception = AppError.Permission("tag discovery denied");

        int exitCode = await TagsCommands.Build(harness.Context).Parse(["summary"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitPermission, exitCode);
        Assert.Equal("tag discovery denied", harness.Error.ToString().Trim());
        Assert.True(harness.Session.Disposed);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Context = new TagsCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<ITagsCommandSession>(Session);
                },
                Output,
                Error);
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public TagsCommandContext Context { get; }
        public int SessionCreations { get; private set; }
    }

    private sealed class FakeSession : ITagsCommandSession
    {
        public IReadOnlyList<TagReference> References { get; set; } = [];
        public IReadOnlyList<TagSummary> Summaries { get; set; } = [];
        public TagMutationResult? Mutation { get; set; }
        public TagCollection Tags { get; set; } = TagCollection.Empty;
        public Tag? LastShowTag { get; private set; }
        public bool LastShowExact { get; private set; }
        public EntityUid? LastTarget { get; private set; }
        public Tag? LastMutationTag { get; private set; }
        public string? LastRemoveKey { get; private set; }
        public Exception? Exception { get; set; }
        public bool Disposed { get; private set; }

        public Task<IReadOnlyList<TagReference>> ShowAsync(
            Tag value,
            bool exact,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastShowTag = value;
            LastShowExact = exact;
            return Result(References);
        }

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result(Summaries);
        }

        public Task<TagMutationResult> UpsertAsync(
            EntityUid target,
            Tag value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTarget = target;
            LastMutationTag = value;
            return Result(Mutation ?? new TagMutationResult(target, new TagCollection([value]), true));
        }

        public Task<TagMutationResult> RemoveAsync(
            EntityUid target,
            string key,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTarget = target;
            LastRemoveKey = key;
            return Result(Mutation ?? new TagMutationResult(target, TagCollection.Empty, false));
        }

        public Task<TagCollection> ListAsync(EntityUid target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTarget = target;
            return Result(Tags);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        private Task<T> Result<T>(T value) => Exception is null
            ? Task.FromResult(value)
            : Task.FromException<T>(Exception);
    }
}
