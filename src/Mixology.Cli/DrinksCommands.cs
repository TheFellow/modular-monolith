using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;

namespace Mixology.Cli;

public interface IDrinksCommandSession : IAsyncDisposable
{
    Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken);

    Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken);

    Task<Drink> CreateAsync(CreateDrinkRequest request, CancellationToken cancellationToken);

    Task<Drink> UpdateAsync(UpdateDrinkRequest request, CancellationToken cancellationToken);

    Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken);
}

public sealed class DrinksCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<IDrinksCommandSession>> createSession,
    TextReader input,
    TextWriter output,
    TextWriter error)
{
    public Func<ParseResult, CancellationToken, ValueTask<IDrinksCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));

    public TextReader Input { get; } = input ?? throw new ArgumentNullException(nameof(input));

    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

    public static DrinksCommandContext FromModule(
        DrinksModule drinks,
        Func<ParseResult, MixologySession> createSession,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(drinks);
        ArgumentNullException.ThrowIfNull(createSession);
        return new DrinksCommandContext(
            (result, _) => ValueTask.FromResult<IDrinksCommandSession>(
                new ModuleCommandSession(drinks, createSession(result))),
            input,
            output,
            error);
    }

    private sealed class ModuleCommandSession(DrinksModule drinks, MixologySession session)
        : IDrinksCommandSession
    {
        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken) =>
            drinks.ListAsync(session, request, cancellationToken);

        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken) =>
            drinks.GetAsync(session, id, cancellationToken);

        public Task<Drink> CreateAsync(CreateDrinkRequest request, CancellationToken cancellationToken) =>
            drinks.CreateAsync(session, request, cancellationToken);

        public Task<Drink> UpdateAsync(UpdateDrinkRequest request, CancellationToken cancellationToken) =>
            drinks.UpdateAsync(session, request, cancellationToken);

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken) =>
            drinks.DeleteAsync(session, id, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class DrinksCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(DrinksCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command drinks = new("drinks", "Manage drinks.");
        drinks.Subcommands.Add(BuildList(context));
        drinks.Subcommands.Add(BuildGet(context));
        drinks.Subcommands.Add(BuildCreate(context));
        drinks.Subcommands.Add(BuildUpdate(context));
        drinks.Subcommands.Add(BuildDelete(context));
        return drinks;
    }

    private static Command BuildList(DrinksCommandContext context)
    {
        Option<bool> json = JsonOption();
        Option<string?> name = new("--name") { Description = "Filter by exact name match" };
        Option<string?> category = new("--category", "-c") { Description = CategoryUsage() };
        Option<string?> glass = new("--glass", "-g") { Description = GlassUsage() };
        Option<string?> filter = new("--filter") { Description = "Filter expression" };
        Option<bool> filterHelp = new("--filter-help") { Description = "Show filter fields and examples" };
        Option<int> limit = new("--limit") { Description = "Number of entries in a cursor page (default 100)" };
        Option<string?> cursor = new("--cursor") { Description = "Continue after a result cursor" };
        Command command = new("list", "List drinks.");
        AddOptions(command, json, name, category, glass, filter, filterHelp, limit, cursor);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(filterHelp))
            {
                await WriteFilterHelpAsync(context.Output).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                Page<Drink> page = await session.ListAsync(
                    new ListDrinksRequest(
                        result.GetValue(name),
                        ParseCategory(result.GetValue(category)),
                        ParseGlass(result.GetValue(glass)),
                        result.GetValue(filter),
                        result.GetValue(cursor),
                        result.GetValue(limit)),
                    cancellationToken).ConfigureAwait(false);
                if (result.GetValue(json))
                {
                    await WriteJsonAsync(
                        context.Output,
                        new DrinkPageView(page.Items.Select(ToView).ToArray(), EmptyToNull(page.Next.Value)))
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

    private static Command BuildGet(DrinksCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Drink ID");
        Option<bool> json = JsonOption();
        Command command = new("get", "Get a drink by ID.");
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Drink drink = await session.GetAsync(
                DrinkId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, ToView(drink)).ConfigureAwait(false);
                return;
            }

            await WriteDetailAsync(context.Output, drink).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildCreate(DrinksCommandContext context)
    {
        Option<bool> stdin = StdinOption();
        Option<string?> file = FileOption();
        Option<bool> template = TemplateOption();
        Option<bool> json = JsonOption();
        Command command = new("create", "Create a new drink from structured JSON.");
        AddOptions(command, stdin, file, template, json);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(template))
            {
                await WriteJsonAsync(context.Output, CreateTemplate()).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(
                context,
                result,
                async session =>
                {
                    CreateDrinkRequest request = ToCreateRequest(
                        await ReadDocumentAsync<CreateDrinkDocument>(context, result, stdin, file, cancellationToken)
                            .ConfigureAwait(false));
                    Drink created = await session.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                    await WriteMutationAsync(context.Output, created, result.GetValue(json)).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildUpdate(DrinksCommandContext context)
    {
        Option<bool> stdin = StdinOption();
        Option<string?> file = FileOption();
        Option<bool> template = TemplateOption();
        Option<bool> json = JsonOption();
        Command command = new("update", "Replace a drink from structured JSON.");
        AddOptions(command, stdin, file, template, json);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(template))
            {
                await WriteJsonAsync(context.Output, UpdateTemplate()).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(
                context,
                result,
                async session =>
                {
                    DrinkDocument document = await ReadDocumentAsync<DrinkDocument>(
                        context,
                        result,
                        stdin,
                        file,
                        cancellationToken).ConfigureAwait(false);
                    Drink updated = await session.UpdateAsync(ToUpdateRequest(document), cancellationToken)
                        .ConfigureAwait(false);
                    await WriteMutationAsync(context.Output, updated, result.GetValue(json)).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildDelete(DrinksCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Drink ID");
        Option<bool> json = JsonOption();
        Command command = new("delete", "Delete a drink by ID.");
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Drink deleted = await session.DeleteAsync(
                DrinkId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, deleted, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static async Task<int> ExecuteAsync(
        DrinksCommandContext context,
        ParseResult result,
        Func<IDrinksCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IDrinksCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static async Task<TDocument> ReadDocumentAsync<TDocument>(
        DrinksCommandContext context,
        ParseResult result,
        Option<bool> stdin,
        Option<string?> file,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        bool fromStdin = result.GetValue(stdin);
        string? path = result.GetValue(file)?.Trim();
        if (fromStdin && !string.IsNullOrEmpty(path))
        {
            throw AppError.Invalid("set only one of --stdin or --file");
        }

        if (!fromStdin && string.IsNullOrEmpty(path))
        {
            throw AppError.Invalid("missing input: set --stdin or --file (or use --template)");
        }

        string source;
        try
        {
            source = fromStdin
                ? await context.Input.ReadToEndAsync(cancellationToken).ConfigureAwait(false)
                : await File.ReadAllTextAsync(path!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppError.Invalid(fromStdin ? "read drink json from stdin" : $"read drink json file \"{path}\"", exception);
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw AppError.Invalid(fromStdin ? "stdin is empty" : $"drink json file \"{path}\" is empty");
        }

        try
        {
            return JsonSerializer.Deserialize<TDocument>(source, JsonOptions)
                ?? throw AppError.Invalid("parse drink json: document is null");
        }
        catch (JsonException exception)
        {
            throw AppError.Invalid($"parse drink json: {exception.Message}", exception);
        }
    }

    private static CreateDrinkRequest ToCreateRequest(CreateDrinkDocument document) => new(
        document.Name ?? string.Empty,
        DrinkCategory.Parse(document.Category),
        GlassType.Parse(document.Glass),
        ToRecipe(document.Recipe),
        document.Description ?? string.Empty);

    private static UpdateDrinkRequest ToUpdateRequest(DrinkDocument document) => new(
        DrinkId.Parse(document.Id ?? string.Empty),
        document.Name ?? string.Empty,
        DrinkCategory.Parse(document.Category),
        GlassType.Parse(document.Glass),
        ToRecipe(document.Recipe),
        document.Description ?? string.Empty,
        document.Revision);

    private static Recipe ToRecipe(RecipeDocument? document)
    {
        if (document is null)
        {
            throw AppError.Invalid("recipe is required");
        }

        RecipeIngredient[] ingredients = (document.Ingredients ?? []).Select(static ingredient =>
        {
            if (ingredient is null)
            {
                throw AppError.Invalid("recipe ingredient is required");
            }

            IngredientId[] substitutes = (ingredient.Substitutes ?? [])
                .Select(static value => IngredientId.Parse(value ?? string.Empty))
                .ToArray();
            return new RecipeIngredient(
                IngredientId.Parse(ingredient.IngredientId ?? string.Empty),
                Amount.Create(ingredient.Amount, Unit.Parse(ingredient.Unit ?? string.Empty)),
                ingredient.Optional,
                substitutes);
        }).ToArray();
        return new Recipe(ingredients, document.Steps ?? [], document.Garnish ?? string.Empty).Normalize();
    }

    private static DrinkCategory? ParseCategory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DrinkCategory.Parse(value);

    private static GlassType? ParseGlass(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : GlassType.Parse(value);

    private static Option<string> RequiredStringOption(string name, string description) =>
        new(name) { Description = description, Required = true };

    private static Option<bool> JsonOption() => new("--json") { Description = "Output JSON" };

    private static Option<bool> StdinOption() => new("--stdin") { Description = "Read JSON from stdin" };

    private static Option<string?> FileOption() => new("--file") { Description = "Read JSON from file" };

    private static Option<bool> TemplateOption() => new("--template") { Description = "Print a JSON template and exit" };

    private static void AddOptions(Command command, params Option[] options)
    {
        foreach (Option option in options)
        {
            command.Options.Add(option);
        }
    }

    private static string CategoryUsage() =>
        $"Category ({string.Join('|', DrinkCategory.All.Select(static value => value.Value))})";

    private static string GlassUsage() =>
        $"Glass ({string.Join('|', GlassType.All.Select(static value => value.Value))})";

    private static async Task WriteMutationAsync(TextWriter output, Drink drink, bool json)
    {
        if (json)
        {
            await WriteJsonAsync(output, ToView(drink)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(drink.Id.Value).ConfigureAwait(false);
    }

    private static async Task WriteTableAsync(TextWriter output, IReadOnlyList<Drink> drinks)
    {
        await output.WriteLineAsync("ID\tNAME\tCATEGORY\tGLASS\tSTATUS\tINGREDIENTS\tTAGS").ConfigureAwait(false);
        foreach (Drink drink in drinks)
        {
            await output.WriteLineAsync(string.Join('\t',
                drink.Id.Value,
                drink.Name,
                drink.Category.Value,
                drink.Glass.Value,
                drink.Status.Value,
                drink.Recipe.Ingredients.Count,
                drink.Tags.Format())).ConfigureAwait(false);
        }
    }

    private static async Task WriteDetailAsync(TextWriter output, Drink drink)
    {
        await output.WriteLineAsync($"ID:\t{drink.Id.Value}").ConfigureAwait(false);
        await output.WriteLineAsync($"Name:\t{drink.Name}").ConfigureAwait(false);
        await output.WriteLineAsync($"Category:\t{drink.Category.Value}").ConfigureAwait(false);
        await output.WriteLineAsync($"Glass:\t{drink.Glass.Value}").ConfigureAwait(false);
        await output.WriteLineAsync($"Status:\t{drink.Status.Value}").ConfigureAwait(false);
        await output.WriteLineAsync($"Ingredients:\t{drink.Recipe.Ingredients.Count}").ConfigureAwait(false);
        await output.WriteLineAsync($"Tags:\t{drink.Tags.Format()}").ConfigureAwait(false);
    }

    private static Task WriteJsonAsync<T>(TextWriter output, T value) =>
        output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));

    private static DrinkView ToView(Drink drink) => new(
        drink.Id.Value,
        drink.Revision,
        drink.Name,
        EmptyToNull(drink.Category.Value),
        EmptyToNull(drink.Glass.Value),
        EmptyToNull(drink.Status.Value),
        EmptyToNull(drink.Description),
        ToView(drink.Recipe),
        drink.Tags.Strings().ToArray());

    private static RecipeView ToView(Recipe recipe) => new(
        recipe.Ingredients.Select(static ingredient => new RecipeIngredientView(
            ingredient.IngredientId.Value,
            ingredient.Amount.Value,
            ingredient.Amount.Unit.Value,
            ingredient.Optional,
            ingredient.Substitutes.Count == 0
                ? null
                : ingredient.Substitutes.Select(static value => value.Value).ToArray())).ToArray(),
        recipe.Steps.ToArray(),
        EmptyToNull(recipe.Garnish));

    private static CreateDrinkDocument CreateTemplate() => new()
    {
        Name = "Margarita",
        Category = DrinkCategory.Cocktail.Value,
        Glass = GlassType.Coupe.Value,
        Description = "A classic sour",
        Recipe = RecipeTemplate(),
    };

    private static DrinkDocument UpdateTemplate() => new()
    {
        Id = "drk-3BxsD9vQRgeYqJ8v4bFVvytN1JU",
        Revision = 1,
        Name = "Margarita",
        Category = DrinkCategory.Cocktail.Value,
        Glass = GlassType.Coupe.Value,
        Description = "A classic sour",
        Recipe = RecipeTemplate(),
    };

    private static RecipeDocument RecipeTemplate() => new()
    {
        Ingredients =
        [
            new RecipeIngredientDocument
            {
                IngredientId = "ing-3BxsD9vQRgeYqJ8v4bFVvytN1JU",
                Amount = 1,
                Unit = Unit.Ounce.Value,
                Substitutes = ["ing-3BxsD9vQRgeYqJ8v4bFVvytN1JV"],
            },
            new RecipeIngredientDocument
            {
                IngredientId = "ing-3BxsD9vQRgeYqJ8v4bFVvytN1JW",
                Amount = 2,
                Unit = Unit.Ounce.Value,
            },
        ],
        Steps = ["Add ingredients to a shaker with ice", "Shake until chilled", "Strain into glass"],
        Garnish = "lime wheel",
    };

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static async Task WriteFilterHelpAsync(TextWriter output)
    {
        await output.WriteLineAsync("FILTER SYNTAX").ConfigureAwait(false);
        await output.WriteLineAsync("  Comparisons: ==  !=  <  <=  >  >=  in  not in").ConfigureAwait(false);
        await output.WriteLineAsync("  Logic:       && / and   || / or   ! / not   (parentheses)").ConfigureAwait(false);
        await output.WriteLineAsync("  Strings:     value.contains(\"x\"), startsWith, endsWith, matches").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("FIELDS").ConfigureAwait(false);
        await output.WriteLineAsync("  id              string        Drink ID").ConfigureAwait(false);
        await output.WriteLineAsync("  name            string        Drink name").ConfigureAwait(false);
        await output.WriteLineAsync("  category        string        Drink category").ConfigureAwait(false);
        await output.WriteLineAsync("  glass           string        Glass type").ConfigureAwait(false);
        await output.WriteLineAsync("  status          string        Lifecycle status").ConfigureAwait(false);
        await output.WriteLineAsync("  description     string        Drink description").ConfigureAwait(false);
        await output.WriteLineAsync("  tags            list<string>  Tags (key or key=value)").ConfigureAwait(false);
        await output.WriteLineAsync("  recipe.garnish  string        Recipe garnish").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("EXAMPLES").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'category == \"cocktail\" && name.contains(\"gin\")'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'glass in [\"coupe\", \"rocks\"] || recipe.garnish.startsWith(\"lemon\")'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'status == \"review_required\"'").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'tags contains \"featured\" || tags contains \"region=west\"'")
            .ConfigureAwait(false);
    }

    private class CreateDrinkDocument
    {
        public string? Name { get; init; }
        public string? Category { get; init; }
        public string? Glass { get; init; }
        public string? Description { get; init; }
        public RecipeDocument? Recipe { get; init; }
    }

    private sealed class DrinkDocument : CreateDrinkDocument
    {
        public string? Id { get; init; }
        public long Revision { get; init; }
    }

    private sealed class RecipeDocument
    {
        public IReadOnlyList<RecipeIngredientDocument?>? Ingredients { get; init; }
        public IReadOnlyList<string>? Steps { get; init; }
        public string? Garnish { get; init; }
    }

    private sealed class RecipeIngredientDocument
    {
        [JsonPropertyName("ingredient_id")]
        public string? IngredientId { get; init; }

        public double Amount { get; init; }
        public string? Unit { get; init; }
        public bool Optional { get; init; }
        public IReadOnlyList<string?>? Substitutes { get; init; }
    }

    private sealed record RecipeIngredientView(
        [property: JsonPropertyName("ingredient_id")] string IngredientId,
        double Amount,
        string Unit,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Optional,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Substitutes);

    private sealed record RecipeView(
        IReadOnlyList<RecipeIngredientView> Ingredients,
        IReadOnlyList<string> Steps,
        string? Garnish);

    private sealed record DrinkView(
        string Id,
        long Revision,
        string Name,
        string? Category,
        string? Glass,
        string? Status,
        string? Description,
        RecipeView Recipe,
        IReadOnlyList<string> Tags);

    private sealed record DrinkPageView(IReadOnlyList<DrinkView> Items, string? Next);
}
