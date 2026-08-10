using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;

namespace Mixology.Cli;

public interface IIngredientsCommandSession : IAsyncDisposable
{
    Task<Page<Ingredient>> ListAsync(ListIngredientsRequest request, CancellationToken cancellationToken);

    Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken);

    Task<Ingredient> CreateAsync(CreateIngredientRequest request, CancellationToken cancellationToken);

    Task<Ingredient> UpdateAsync(UpdateIngredientRequest request, CancellationToken cancellationToken);

    Task<Ingredient> RetireAsync(RetireIngredientRequest request, CancellationToken cancellationToken);
}

public sealed class IngredientsCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<IIngredientsCommandSession>> createSession,
    TextWriter output,
    TextWriter error)
{
    public Func<ParseResult, CancellationToken, ValueTask<IIngredientsCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));

    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

    public static IngredientsCommandContext FromModule(
        IngredientsModule ingredients,
        Func<ParseResult, MixologySession> createSession,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(createSession);
        return new IngredientsCommandContext(
            (result, _) => ValueTask.FromResult<IIngredientsCommandSession>(
                new ModuleCommandSession(ingredients, createSession(result))),
            output,
            error);
    }

    private sealed class ModuleCommandSession(IngredientsModule ingredients, MixologySession session)
        : IIngredientsCommandSession
    {
        public Task<Page<Ingredient>> ListAsync(
            ListIngredientsRequest request,
            CancellationToken cancellationToken) =>
            ingredients.ListAsync(session, request, cancellationToken);

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken) =>
            ingredients.GetAsync(session, id, cancellationToken);

        public Task<Ingredient> CreateAsync(
            CreateIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.CreateAsync(session, request, cancellationToken);

        public Task<Ingredient> UpdateAsync(
            UpdateIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.UpdateAsync(session, request, cancellationToken);

        public Task<Ingredient> RetireAsync(
            RetireIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.RetireAsync(session, request, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class IngredientsCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(IngredientsCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command ingredients = new("ingredients", "Manage ingredients.");
        ingredients.Subcommands.Add(BuildList(context));
        ingredients.Subcommands.Add(BuildGet(context));
        ingredients.Subcommands.Add(BuildCreate(context));
        ingredients.Subcommands.Add(BuildUpdate(context));
        ingredients.Subcommands.Add(BuildRetire(context));
        return ingredients;
    }

    private static Command BuildList(IngredientsCommandContext context)
    {
        Option<bool> json = JsonOption();
        Option<string?> category = new("--category", "-c") { Description = CategoryUsage() };
        Option<string?> filter = new("--filter") { Description = "Filter expression" };
        Option<bool> filterHelp = new("--filter-help") { Description = "Show filter fields and examples" };
        Option<int> limit = new("--limit") { Description = "Number of entries in a cursor page (default 100)" };
        Option<string?> cursor = new("--cursor") { Description = "Continue after a result cursor" };
        Command command = new("list", "List ingredients.");
        AddOptions(command, json, category, filter, filterHelp, limit, cursor);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(filterHelp))
            {
                await WriteFilterHelpAsync(context.Output).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                IngredientCategory? selectedCategory = ParseCategory(result.GetValue(category));
                Page<Ingredient> page = await session.ListAsync(
                    new ListIngredientsRequest(
                        selectedCategory,
                        result.GetValue(filter),
                        result.GetValue(cursor),
                        result.GetValue(limit)),
                    cancellationToken).ConfigureAwait(false);
                if (result.GetValue(json))
                {
                    await WriteJsonAsync(
                        context.Output,
                        new IngredientPageView(page.Items.Select(ToView).ToArray(), EmptyToNull(page.Next.Value)))
                        .ConfigureAwait(false);
                    return;
                }

                await WriteTableAsync(context.Output, page.Items).ConfigureAwait(false);
                if (!page.Next.IsEmpty)
                {
                    await context.Output.WriteLineAsync($"Next cursor: {page.Next}").ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildGet(IngredientsCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Ingredient ID");
        Option<bool> json = JsonOption();
        Command command = new("get", "Get an ingredient by ID.");
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Ingredient ingredient = await session.GetAsync(
                IngredientId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, ToView(ingredient)).ConfigureAwait(false);
                return;
            }

            await WriteDetailAsync(context.Output, ingredient).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildCreate(IngredientsCommandContext context)
    {
        Argument<string?> name = new("name")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Ingredient name",
        };
        Option<string> category = RequiredStringOption("--category", CategoryUsage(), "-c");
        Option<string> unit = RequiredStringOption("--unit", UnitUsage(), "-u");
        Option<string?> description = new("--description", "-d") { Description = "Description" };
        Option<bool> json = JsonOption();
        Command command = new("create", "Create a new ingredient.");
        command.Arguments.Add(name);
        AddOptions(command, category, unit, description, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Ingredient created = await session.CreateAsync(
                new CreateIngredientRequest(
                    result.GetValue(name) ?? string.Empty,
                    IngredientCategory.Parse(result.GetRequiredValue(category)),
                    Unit.Parse(result.GetRequiredValue(unit)),
                    result.GetValue(description) ?? string.Empty),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, created, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildUpdate(IngredientsCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Ingredient ID");
        Option<string?> name = new("--name", "-n") { Description = "New name" };
        Option<string?> category = new("--category", "-c") { Description = CategoryUsage() };
        Option<string?> unit = new("--unit", "-u") { Description = UnitUsage() };
        Option<string?> description = new("--description", "-d") { Description = "Description" };
        Option<bool> json = JsonOption();
        Command command = new("update", "Update an ingredient.");
        AddOptions(command, id, name, category, unit, description, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Ingredient updated = await session.UpdateAsync(
                new UpdateIngredientRequest(
                    IngredientId.Parse(result.GetRequiredValue(id)),
                    result.GetValue(name),
                    ParseCategory(result.GetValue(category)),
                    ParseUnit(result.GetValue(unit)),
                    result.GetValue(description)),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, updated, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildRetire(IngredientsCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Ingredient ID");
        Option<string?> replacementId = new("--replacement-id")
        {
            Description = "Explicit permanent replacement ingredient ID",
        };
        Option<double> replacementRatio = new("--replacement-ratio")
        {
            Description = "Replacement quantity ratio (defaults to 1)",
        };
        Option<bool> json = JsonOption();
        Command command = new("retire", "Retire an ingredient and mark dependent drinks for review.");
        command.Aliases.Add("delete");
        AddOptions(command, id, replacementId, replacementRatio, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            string? replacement = result.GetValue(replacementId);
            Ingredient retired = await session.RetireAsync(
                new RetireIngredientRequest(
                    IngredientId.Parse(result.GetRequiredValue(id)),
                    new Retirement(
                        string.IsNullOrWhiteSpace(replacement) ? null : IngredientId.Parse(replacement.Trim()),
                        result.GetValue(replacementRatio))),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, retired, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static async Task<int> ExecuteAsync(
        IngredientsCommandContext context,
        ParseResult result,
        Func<IIngredientsCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IIngredientsCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static IngredientCategory? ParseCategory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : IngredientCategory.Parse(value);

    private static Unit? ParseUnit(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Unit.Parse(value);

    private static Option<string> RequiredStringOption(string name, string description, params string[] aliases) =>
        new(name, aliases) { Description = description, Required = true };

    private static Option<bool> JsonOption() => new("--json") { Description = "Output JSON" };

    private static void AddOptions(Command command, params Option[] options)
    {
        foreach (Option option in options)
        {
            command.Options.Add(option);
        }
    }

    private static string CategoryUsage() =>
        $"Category ({string.Join('|', IngredientCategory.All.Select(static value => value.Value))})";

    private static string UnitUsage() =>
        $"Unit ({string.Join('|', Unit.All.Select(static value => value.Value))})";

    private static async Task WriteMutationAsync(TextWriter output, Ingredient ingredient, bool json)
    {
        if (json)
        {
            await WriteJsonAsync(output, ToView(ingredient)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(ingredient.Id.Value).ConfigureAwait(false);
    }

    private static async Task WriteTableAsync(TextWriter output, IReadOnlyList<Ingredient> ingredients)
    {
        await output.WriteLineAsync("ID\tNAME\tCATEGORY\tUNIT\tDESCRIPTION\tTAGS").ConfigureAwait(false);
        foreach (Ingredient ingredient in ingredients)
        {
            await output.WriteLineAsync(string.Join('\t',
                ingredient.Id.Value,
                ingredient.Name,
                ingredient.Category.Value,
                ingredient.Unit.Value,
                ingredient.Description,
                ingredient.Tags.Format())).ConfigureAwait(false);
        }
    }

    private static async Task WriteDetailAsync(TextWriter output, Ingredient ingredient)
    {
        IngredientView view = ToView(ingredient);
        await output.WriteLineAsync($"ID:\t{view.Id}").ConfigureAwait(false);
        await output.WriteLineAsync($"Name:\t{view.Name}").ConfigureAwait(false);
        await output.WriteLineAsync($"Category:\t{view.Category}").ConfigureAwait(false);
        await output.WriteLineAsync($"Unit:\t{view.Unit}").ConfigureAwait(false);
        if (!string.IsNullOrEmpty(view.Description))
        {
            await output.WriteLineAsync($"Description:\t{view.Description}").ConfigureAwait(false);
        }

        await output.WriteLineAsync($"Tags:\t{string.Join(',', view.Tags)}").ConfigureAwait(false);
    }

    private static Task WriteJsonAsync<T>(TextWriter output, T value) =>
        output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));

    private static IngredientView ToView(Ingredient ingredient) => new(
        ingredient.Id.Value,
        ingredient.Name,
        ingredient.Category.Value,
        ingredient.Unit.Value,
        ingredient.Description,
        ingredient.Tags.Strings().ToArray());

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static async Task WriteFilterHelpAsync(TextWriter output)
    {
        await output.WriteLineAsync("FILTER SYNTAX").ConfigureAwait(false);
        await output.WriteLineAsync("  Comparisons: ==  !=  <  <=  >  >=  in  not in").ConfigureAwait(false);
        await output.WriteLineAsync("  Logic:       && / and   || / or   ! / not   (parentheses)").ConfigureAwait(false);
        await output.WriteLineAsync("  Strings:     value.contains(\"x\"), startsWith, endsWith, matches").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("FIELDS").ConfigureAwait(false);
        await output.WriteLineAsync("  id           string        Ingredient ID").ConfigureAwait(false);
        await output.WriteLineAsync("  name         string        Ingredient name").ConfigureAwait(false);
        await output.WriteLineAsync("  category     string        Ingredient category").ConfigureAwait(false);
        await output.WriteLineAsync("  unit         string        Measurement unit").ConfigureAwait(false);
        await output.WriteLineAsync("  description  string        Ingredient description").ConfigureAwait(false);
        await output.WriteLineAsync("  tags         list<string>  Tags (key or key=value)").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("EXAMPLES").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'category == \"spirit\" && name.contains(\"gin\")'").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'unit in [\"ml\", \"oz\"] && !description.contains(\"seasonal\")'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'tags contains \"featured\" || tags contains \"region=west\"'")
            .ConfigureAwait(false);
    }

    private sealed record IngredientView(
        string Id,
        string Name,
        string Category,
        string Unit,
        string Description,
        IReadOnlyList<string> Tags);

    private sealed record IngredientPageView(IReadOnlyList<IngredientView> Items, string? Next);
}
