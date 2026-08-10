using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Cli;

public interface ITagsCommandSession : IAsyncDisposable
{
    Task<IReadOnlyList<TagReference>> ShowAsync(
        Tag value,
        bool exact,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken);

    Task<TagMutationResult> UpsertAsync(
        EntityUid target,
        Tag value,
        CancellationToken cancellationToken);

    Task<TagMutationResult> RemoveAsync(
        EntityUid target,
        string key,
        CancellationToken cancellationToken);

    Task<TagCollection> ListAsync(EntityUid target, CancellationToken cancellationToken);
}

public sealed class TagsCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<ITagsCommandSession>> createSession,
    TextWriter output,
    TextWriter error)
{
    public Func<ParseResult, CancellationToken, ValueTask<ITagsCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));

    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

    public static TagsCommandContext FromModule(
        TaggingModule tagging,
        Func<ParseResult, MixologySession> createSession,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(tagging);
        ArgumentNullException.ThrowIfNull(createSession);
        return new TagsCommandContext(
            (result, _) => ValueTask.FromResult<ITagsCommandSession>(
                new ModuleCommandSession(tagging, createSession(result))),
            output,
            error);
    }

    private sealed class ModuleCommandSession(TaggingModule tagging, MixologySession session)
        : ITagsCommandSession
    {
        public Task<IReadOnlyList<TagReference>> ShowAsync(
            Tag value,
            bool exact,
            CancellationToken cancellationToken) =>
            tagging.ShowAsync(session, value, exact, cancellationToken);

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken) =>
            tagging.SummaryAsync(session, cancellationToken);

        public Task<TagMutationResult> UpsertAsync(
            EntityUid target,
            Tag value,
            CancellationToken cancellationToken) =>
            tagging.UpsertAsync(session, target, value, cancellationToken);

        public Task<TagMutationResult> RemoveAsync(
            EntityUid target,
            string key,
            CancellationToken cancellationToken) =>
            tagging.RemoveAsync(session, target, key, cancellationToken);

        public Task<TagCollection> ListAsync(EntityUid target, CancellationToken cancellationToken) =>
            tagging.ListAsync(session, target, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class TagsCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(TagsCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command tags = new("tags", "Manage entity tags.");
        tags.Subcommands.Add(BuildShow(context));
        tags.Subcommands.Add(BuildSummary(context));
        tags.Subcommands.Add(BuildAdd(context));
        tags.Subcommands.Add(BuildRemove(context));
        tags.Subcommands.Add(BuildList(context));
        return tags;
    }

    private static Command BuildShow(TagsCommandContext context)
    {
        Argument<string?> tag = OptionalArgument("tag", "Tag in key or key=value form");
        Option<string?> key = new("--key") { Description = "Match every value for this tag key" };
        Option<bool> json = JsonOption();
        Command command = new("show", "Show active entities referencing a tag.");
        command.Arguments.Add(tag);
        AddOptions(command, key, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            string? rawTag = TrimToNull(result.GetValue(tag));
            string? rawKey = TrimToNull(result.GetValue(key));
            if (rawTag is null && rawKey is null)
            {
                throw AppError.Invalid("tag argument or --key is required");
            }

            if (rawTag is not null && rawKey is not null)
            {
                throw AppError.Invalid("tag argument and --key cannot be used together");
            }

            bool exact = rawTag is not null;
            Tag value = exact ? Tag.Parse(rawTag!) : Tag.Create(rawKey!);
            IReadOnlyList<TagReference> rows = await session.ShowAsync(value, exact, cancellationToken)
                .ConfigureAwait(false);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, rows).ConfigureAwait(false);
                return;
            }

            await WriteReferencesAsync(context.Output, rows).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildSummary(TagsCommandContext context)
    {
        Option<bool> json = JsonOption();
        Command command = new("summary", "Summarize active tag usage.");
        command.Options.Add(json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            IReadOnlyList<TagSummary> rows = await session.SummaryAsync(cancellationToken).ConfigureAwait(false);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, rows).ConfigureAwait(false);
                return;
            }

            await WriteSummariesAsync(context.Output, rows).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildAdd(TagsCommandContext context)
    {
        Argument<string?> target = OptionalArgument("entity-id", "Entity ID");
        Argument<string?> tag = OptionalArgument("tag", "Tag in key or key=value form");
        Option<bool> json = JsonOption();
        Command command = new("add", "Add or replace a tag.");
        command.Arguments.Add(target);
        command.Arguments.Add(tag);
        command.Options.Add(json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            EntityUid uid = ParseTarget(RequiredArgument(result.GetValue(target), "entity-id"));
            Tag value = Tag.Parse(RequiredArgument(result.GetValue(tag), "tag"));
            TagMutationResult mutation = await session.UpsertAsync(uid, value, cancellationToken)
                .ConfigureAwait(false);
            await WriteMutationAsync(context.Output, mutation, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildRemove(TagsCommandContext context)
    {
        Argument<string?> target = OptionalArgument("entity-id", "Entity ID");
        Argument<string?> key = OptionalArgument("key", "Tag key");
        Option<bool> json = JsonOption();
        Command command = new("remove", "Remove a tag by key.");
        command.Arguments.Add(target);
        command.Arguments.Add(key);
        command.Options.Add(json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            EntityUid uid = ParseTarget(RequiredArgument(result.GetValue(target), "entity-id"));
            string value = RequiredArgument(result.GetValue(key), "key");
            TagMutationResult mutation = await session.RemoveAsync(uid, value, cancellationToken)
                .ConfigureAwait(false);
            await WriteMutationAsync(context.Output, mutation, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildList(TagsCommandContext context)
    {
        Argument<string?> target = OptionalArgument("entity-id", "Entity ID");
        Option<bool> json = JsonOption();
        Command command = new("list", "List tags on an entity.");
        command.Arguments.Add(target);
        command.Options.Add(json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            EntityUid uid = ParseTarget(RequiredArgument(result.GetValue(target), "entity-id"));
            TagCollection values = await session.ListAsync(uid, cancellationToken).ConfigureAwait(false);
            TagStateView view = new(uid.Id, values.Strings(), null);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, view).ConfigureAwait(false);
                return;
            }

            await WriteStateAsync(context.Output, view, null).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static async Task<int> ExecuteAsync(
        TagsCommandContext context,
        ParseResult result,
        Func<ITagsCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using ITagsCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static EntityUid ParseTarget(string source)
    {
        try
        {
            return EntityIds.Parse(source);
        }
        catch (Exception exception) when (AppError.IsInvalid(exception))
        {
            throw AppError.Invalid($"invalid entity-id \"{source}\"", exception);
        }
    }

    private static string RequiredArgument(string? source, string name) =>
        TrimToNull(source) ?? throw AppError.Invalid($"{name} argument is required");

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Argument<string?> OptionalArgument(string name, string description) => new(name)
    {
        Arity = ArgumentArity.ZeroOrOne,
        Description = description,
    };

    private static Option<bool> JsonOption() => new("--json") { Description = "Output JSON" };

    private static void AddOptions(Command command, params Option[] options)
    {
        foreach (Option option in options)
        {
            command.Options.Add(option);
        }
    }

    private static async Task WriteMutationAsync(TextWriter output, TagMutationResult mutation, bool json)
    {
        TagStateView view = new(mutation.Target.Id, mutation.Tags.Strings(), mutation.Changed);
        if (json)
        {
            await WriteJsonAsync(output, view).ConfigureAwait(false);
            return;
        }

        await WriteStateAsync(output, view, mutation.Changed ? "changed" : "unchanged").ConfigureAwait(false);
    }

    private static async Task WriteStateAsync(TextWriter output, TagStateView view, string? state)
    {
        string values = view.Tags.Count == 0 ? "(none)" : string.Join(',', view.Tags);
        string suffix = state is null ? string.Empty : $" ({state})";
        await output.WriteLineAsync($"{view.EntityId}: {values}{suffix}").ConfigureAwait(false);
    }

    private static async Task WriteReferencesAsync(TextWriter output, IReadOnlyList<TagReference> rows)
    {
        await output.WriteLineAsync("ENTITY_TYPE\tENTITY_NAME\tENTITY_ID\tTAG").ConfigureAwait(false);
        foreach (TagReference row in rows)
        {
            await output.WriteLineAsync(
                $"{row.EntityType}\t{row.EntityName}\t{row.EntityId}\t{row.Tag}").ConfigureAwait(false);
        }
    }

    private static async Task WriteSummariesAsync(TextWriter output, IReadOnlyList<TagSummary> rows)
    {
        await output.WriteLineAsync("TAG\tTOTAL\tDRINKS\tINGREDIENTS\tINVENTORY\tMENUS\tORDERS")
            .ConfigureAwait(false);
        foreach (TagSummary row in rows)
        {
            await output.WriteLineAsync(
                $"{row.Tag}\t{row.Total}\t{row.Drinks}\t{row.Ingredients}\t{row.Inventory}\t{row.Menus}\t{row.Orders}")
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonAsync<T>(TextWriter output, T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        await output.WriteLineAsync(json).ConfigureAwait(false);
    }

    private sealed record TagStateView(
        string EntityId,
        IReadOnlyList<string> Tags,
        bool? Changed);
}
