using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;

namespace Mixology.Cli;

public interface IMenusCommandSession : IAsyncDisposable
{
    Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken);
    Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken);
    Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken);
    Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken);
    Task<Menu> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken);
    Task<Menu> UpdateAsync(UpdateMenuRequest request, CancellationToken cancellationToken);
    Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken);
    Task<Menu> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken);
    Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken);
    Task<Menu> PublishAsync(MenuId id, CancellationToken cancellationToken);
    Task<Menu> DraftAsync(MenuId id, CancellationToken cancellationToken);
}

public sealed class MenusCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<IMenusCommandSession>> createSession,
    TextWriter output,
    TextWriter error,
    TextReader? input = null)
{
    public Func<ParseResult, CancellationToken, ValueTask<IMenusCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));
    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));
    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));
    public TextReader Input { get; } = input ?? Console.In;

    public static MenusCommandContext FromModule(
        MenusModule menus,
        Func<ParseResult, MixologySession> createSession,
        TextWriter output,
        TextWriter error,
        TextReader? input = null)
    {
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(createSession);
        return new MenusCommandContext(
            (result, _) => ValueTask.FromResult<IMenusCommandSession>(
                new ModuleCommandSession(menus, createSession(result))),
            output,
            error,
            input);
    }

    private sealed class ModuleCommandSession(MenusModule menus, MixologySession session) : IMenusCommandSession
    {
        public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken) =>
            menus.ListAsync(session, request, cancellationToken);
        public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.GetAsync(session, id, cancellationToken);
        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.ReadinessAsync(session, id, cancellationToken);
        public Task<MenuAnalysis> AnalyzeAsync(MenuId id, double targetMargin, CancellationToken cancellationToken) =>
            menus.AnalyzeAsync(session, id, targetMargin, cancellationToken);
        public Task<Menu> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken) =>
            menus.CreateAsync(session, request, cancellationToken);
        public Task<Menu> UpdateAsync(UpdateMenuRequest request, CancellationToken cancellationToken) =>
            menus.UpdateAsync(session, request, cancellationToken);
        public Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.DeleteAsync(session, id, cancellationToken);
        public Task<Menu> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken) =>
            menus.AddDrinkAsync(session, request, cancellationToken);
        public Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken) =>
            menus.RemoveDrinkAsync(session, request, cancellationToken);
        public Task<Menu> PublishAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.PublishAsync(session, id, cancellationToken);
        public Task<Menu> DraftAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.DraftAsync(session, id, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class MenusCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(MenusCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command menus = new("menus", "Curate drink menus.");
        menus.Subcommands.Add(BuildReadiness(context));
        menus.Subcommands.Add(BuildList(context));
        menus.Subcommands.Add(BuildShow(context));
        menus.Subcommands.Add(BuildCreate(context));
        menus.Subcommands.Add(BuildUpdate(context));
        menus.Subcommands.Add(BuildDelete(context));
        menus.Subcommands.Add(BuildAddDrink(context));
        menus.Subcommands.Add(BuildRemoveDrink(context));
        menus.Subcommands.Add(BuildPublish(context));
        menus.Subcommands.Add(BuildDraft(context));
        return menus;
    }

    private static Command BuildReadiness(MenusCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Menu ID");
        Option<bool> json = JsonOption();
        Command command = new("readiness", "Report publication blockers and operational warnings.");
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            ReadinessReport report = await session.ReadinessAsync(
                MenuId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, ToView(report)).ConfigureAwait(false);
                return;
            }

            if (report.Findings.Count == 0)
            {
                await context.Output.WriteLineAsync("ready: no findings").ConfigureAwait(false);
                return;
            }

            foreach (ReadinessFinding finding in report.Findings)
            {
                await context.Output.WriteLineAsync(
                    $"{finding.Severity.Value}\t{finding.Code.Value}\t{finding.Message}").ConfigureAwait(false);
            }
        }, cancellationToken));
        return command;
    }

    private static Command BuildList(MenusCommandContext context)
    {
        Option<bool> json = JsonOption();
        Option<bool> costs = CostsOption();
        Option<string> margin = TargetMarginOption();
        Option<string?> status = new("--status") { Description = "Status (draft|published|archived)" };
        Option<string?> filter = new("--filter") { Description = "Filter expression" };
        Option<bool> filterHelp = new("--filter-help") { Description = "Show filter fields and examples" };
        Option<int> limit = new("--limit") { Description = "Number of entries in a cursor page (default 100)" };
        Option<string?> cursor = new("--cursor") { Description = "Continue after a result cursor" };
        Command command = new("list", "List menus.");
        AddOptions(command, json, costs, margin, status, filter, filterHelp, limit, cursor);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(filterHelp))
            {
                await WriteFilterHelpAsync(context.Output).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                Page<Menu> page = await session.ListAsync(
                    new ListMenusRequest(
                        ParseStatus(result.GetValue(status)),
                        result.GetValue(filter),
                        result.GetValue(cursor),
                        result.GetValue(limit)),
                    cancellationToken).ConfigureAwait(false);
                double targetMargin = ParseTargetMargin(result.GetRequiredValue(margin));
                if (result.GetValue(json))
                {
                    await WriteJsonAsync(
                        context.Output,
                        new MenuPageView(page.Items.Select(ToView).ToArray(), EmptyToNull(page.Next.Value)))
                        .ConfigureAwait(false);
                    return;
                }

                await WriteMenuTableAsync(context.Output, page.Items).ConfigureAwait(false);
                if (result.GetValue(costs))
                {
                    foreach (Menu menu in page.Items.Where(static menu => menu.Items.Count != 0))
                    {
                        MenuAnalysis analysis = await session.AnalyzeAsync(
                            menu.Id,
                            targetMargin,
                            cancellationToken).ConfigureAwait(false);
                        string average = analysis.AverageMargin is { } value
                            ? $"; avg margin: {FormatPercent(value)}"
                            : string.Empty;
                        await context.Output.WriteLineAsync(
                            $"{menu.Id}\tavailable: {analysis.AvailableCount}/{analysis.TotalCount}{average}")
                            .ConfigureAwait(false);
                    }
                }

                if (!page.Next.IsEmpty)
                {
                    await context.Output.WriteLineAsync($"Next cursor: {page.Next}").ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildShow(MenusCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Menu ID");
        Option<bool> json = JsonOption();
        Option<bool> costs = CostsOption();
        Option<string> margin = TargetMarginOption();
        Command command = new("show", "Show a menu.");
        AddOptions(command, id, json, costs, margin);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            MenuId menuId = MenuId.Parse(result.GetRequiredValue(id));
            double targetMargin = ParseTargetMargin(result.GetRequiredValue(margin));
            Menu menu = await session.GetAsync(menuId, cancellationToken).ConfigureAwait(false);
            if (result.GetValue(costs))
            {
                MenuAnalysis analysis = await session.AnalyzeAsync(
                    menuId,
                    targetMargin,
                    cancellationToken).ConfigureAwait(false);
                if (result.GetValue(json))
                {
                    await WriteJsonAsync(context.Output, ToView(analysis)).ConfigureAwait(false);
                    return;
                }

                await WriteMenuDetailAsync(context.Output, menu).ConfigureAwait(false);
                await WriteAnalysisAsync(context.Output, analysis).ConfigureAwait(false);
                return;
            }

            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, ToView(menu)).ConfigureAwait(false);
                return;
            }

            await WriteMenuDetailAsync(context.Output, menu).ConfigureAwait(false);
            if (menu.Items.Count != 0)
            {
                await context.Output.WriteLineAsync().ConfigureAwait(false);
                await WriteItemTableAsync(context.Output, menu.Items).ConfigureAwait(false);
            }
        }, cancellationToken));
        return command;
    }

    private static Command BuildCreate(MenusCommandContext context)
    {
        Argument<string?> name = new("name") { Arity = ArgumentArity.ZeroOrOne, Description = "Menu name" };
        Option<bool> json = JsonOption();
        Option<bool> template = TemplateOption();
        Option<bool> stdin = StdinOption();
        Option<string?> file = FileOption();
        Command command = new("create", "Create a new menu.");
        command.Arguments.Add(name);
        AddOptions(command, json, template, stdin, file);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(template))
            {
                await WriteJsonAsync(
                    context.Output,
                    new MenuInput(null, 0, "Summer Cocktails", "Refreshing drinks for warm weather"))
                    .ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                MenuInput input = UsesStructuredInput(result, stdin, file)
                    ? await ReadInputAsync(context, result, stdin, file, cancellationToken).ConfigureAwait(false)
                    : new MenuInput(null, 0, result.GetValue(name) ?? string.Empty, string.Empty);
                Menu created = await session.CreateAsync(
                    new CreateMenuRequest(input.Name ?? string.Empty, input.Description ?? string.Empty),
                    cancellationToken).ConfigureAwait(false);
                await WriteMutationAsync(context.Output, created, result.GetValue(json)).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildUpdate(MenusCommandContext context)
    {
        Option<string?> id = new("--id") { Description = "Menu ID" };
        Option<string?> name = new("--name", "-n") { Description = "New name" };
        Option<string?> description = new("--description", "-d")
        {
            Description = "New non-empty description; blank preserves the current description",
        };
        Option<bool> json = JsonOption();
        Option<bool> template = TemplateOption();
        Option<bool> stdin = StdinOption();
        Option<string?> file = FileOption();
        Command command = new("update", "Rename a draft menu or update its non-empty description.");
        AddOptions(command, id, name, description, json, template, stdin, file);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(template))
            {
                await WriteJsonAsync(
                    context.Output,
                    new MenuInput("mnu-...", 1, "Summer Cocktails", "Refreshing drinks for warm weather"))
                    .ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                MenuInput input;
                if (UsesStructuredInput(result, stdin, file))
                {
                    input = await ReadInputAsync(context, result, stdin, file, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    string rawId = result.GetValue(id) ?? throw AppError.Invalid("id is required (or use --stdin/--file)");
                    if (result.GetResult(name) is null && result.GetResult(description) is null)
                    {
                        throw AppError.Invalid("at least one of name or description is required");
                    }

                    Menu current = await session.GetAsync(MenuId.Parse(rawId), cancellationToken).ConfigureAwait(false);
                    input = new MenuInput(
                        current.Id.Value,
                        current.Revision,
                        result.GetValue(name) ?? current.Name,
                        result.GetValue(description) ?? current.Description);
                }

                Menu updated = await session.UpdateAsync(
                    new UpdateMenuRequest(
                        MenuId.Parse(input.Id ?? string.Empty),
                        input.Name ?? string.Empty,
                        input.Description ?? string.Empty,
                        input.Revision),
                    cancellationToken).ConfigureAwait(false);
                await WriteMutationAsync(context.Output, updated, result.GetValue(json)).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildDelete(MenusCommandContext context) =>
        BuildIdMutation("delete", "Delete a draft menu.", context, static (session, id, token) =>
            session.DeleteAsync(id, token));

    private static Command BuildPublish(MenusCommandContext context) =>
        BuildIdMutation("publish", "Publish a menu.", context, static (session, id, token) =>
            session.PublishAsync(id, token));

    private static Command BuildDraft(MenusCommandContext context) =>
        BuildIdMutation("draft", "Return a published menu to draft status.", context, static (session, id, token) =>
            session.DraftAsync(id, token));

    private static Command BuildIdMutation(
        string name,
        string description,
        MenusCommandContext context,
        Func<IMenusCommandSession, MenuId, CancellationToken, Task<Menu>> mutation)
    {
        Option<string> id = RequiredStringOption("--id", "Menu ID");
        Option<bool> json = JsonOption();
        Command command = new(name, description);
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Menu menu = await mutation(
                session,
                MenuId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, menu, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildAddDrink(MenusCommandContext context) =>
        BuildDrinkMutation("add-drink", "Add a drink to a menu.", context, static (session, request, token) =>
            session.AddDrinkAsync(new AddMenuItemRequest(request.MenuId, request.DrinkId), token));

    private static Command BuildRemoveDrink(MenusCommandContext context) =>
        BuildDrinkMutation("remove-drink", "Remove a drink from a menu.", context, static (session, request, token) =>
            session.RemoveDrinkAsync(new RemoveMenuItemRequest(request.MenuId, request.DrinkId), token));

    private static Command BuildDrinkMutation(
        string name,
        string description,
        MenusCommandContext context,
        Func<IMenusCommandSession, MenuDrinkInput, CancellationToken, Task<Menu>> mutation)
    {
        Option<string> menuId = RequiredStringOption("--menu-id", "Menu ID");
        Option<string> drinkId = RequiredStringOption("--drink-id", "Drink ID");
        Option<bool> json = JsonOption();
        Command command = new(name, description);
        AddOptions(command, menuId, drinkId, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Menu menu = await mutation(
                session,
                new MenuDrinkInput(
                    MenuId.Parse(result.GetRequiredValue(menuId)),
                    DrinkId.Parse(result.GetRequiredValue(drinkId))),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, menu, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static bool UsesStructuredInput(
        ParseResult result,
        Option<bool> stdin,
        Option<string?> file) =>
        result.GetValue(stdin) || !string.IsNullOrWhiteSpace(result.GetValue(file));

    private static async Task<MenuInput> ReadInputAsync(
        MenusCommandContext context,
        ParseResult result,
        Option<bool> stdin,
        Option<string?> file,
        CancellationToken cancellationToken)
    {
        bool useStdin = result.GetValue(stdin);
        string? path = result.GetValue(file);
        if (useStdin && !string.IsNullOrWhiteSpace(path))
        {
            throw AppError.Invalid("choose either --stdin or --file");
        }

        string json = useStdin
            ? await context.Input.ReadToEndAsync(cancellationToken).ConfigureAwait(false)
            : await File.ReadAllTextAsync(path?.Trim() ?? string.Empty, cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<MenuInput>(json, JsonOptions)
                ?? throw AppError.Invalid("parse menu json: input is empty");
        }
        catch (JsonException exception)
        {
            throw AppError.Invalid("parse menu json", exception);
        }
    }

    private static async Task<int> ExecuteAsync(
        MenusCommandContext context,
        ParseResult result,
        Func<IMenusCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IMenusCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static MenuStatus? ParseStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : MenuStatus.Parse(value);

    private static double ParseTargetMargin(string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value) || value is <= 0 or >= 1)
        {
            throw AppError.Invalid("target margin must be a number between 0 and 1");
        }

        return value;
    }

    private static Option<string> RequiredStringOption(string name, string description, params string[] aliases) =>
        new(name, aliases) { Description = description, Required = true };
    private static Option<bool> JsonOption() => new("--json") { Description = "Output JSON" };
    private static Option<bool> CostsOption() => new("--costs") { Description = "Include cost/margin analytics" };
    private static Option<string> TargetMarginOption() => new("--target-margin")
    {
        Description = "Target margin for suggested prices (0-1)",
        DefaultValueFactory = _ => "0.7",
    };
    private static Option<bool> TemplateOption() => new("--template") { Description = "Write a JSON input template" };
    private static Option<bool> StdinOption() => new("--stdin") { Description = "Read JSON input from standard input" };
    private static Option<string?> FileOption() => new("--file") { Description = "Read JSON input from a file" };

    private static void AddOptions(Command command, params Option[] options)
    {
        foreach (Option option in options)
        {
            command.Options.Add(option);
        }
    }

    private static async Task WriteMutationAsync(TextWriter output, Menu menu, bool json)
    {
        if (json)
        {
            await WriteJsonAsync(output, ToView(menu)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(menu.Id.Value).ConfigureAwait(false);
    }

    private static async Task WriteMenuTableAsync(TextWriter output, IReadOnlyList<Menu> menus)
    {
        await output.WriteLineAsync("ID\tNAME\tSTATUS\tITEMS\tCREATED_AT\tPUBLISHED_AT\tTAGS").ConfigureAwait(false);
        foreach (Menu menu in menus)
        {
            await output.WriteLineAsync(string.Join('\t',
                menu.Id.Value,
                menu.Name,
                menu.Status.Value,
                menu.Items.Count.ToString(CultureInfo.InvariantCulture),
                FormatTime(menu.CreatedAt),
                menu.PublishedAt is { } published ? FormatTime(published) : string.Empty,
                menu.Tags.Format())).ConfigureAwait(false);
        }
    }

    private static async Task WriteMenuDetailAsync(TextWriter output, Menu menu)
    {
        await output.WriteLineAsync($"ID:\t{menu.Id}").ConfigureAwait(false);
        await output.WriteLineAsync($"Name:\t{menu.Name}").ConfigureAwait(false);
        await output.WriteLineAsync($"Status:\t{menu.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Items:\t{menu.Items.Count}").ConfigureAwait(false);
        await output.WriteLineAsync($"Created at:\t{FormatTime(menu.CreatedAt)}").ConfigureAwait(false);
        if (menu.PublishedAt is { } published)
        {
            await output.WriteLineAsync($"Published at:\t{FormatTime(published)}").ConfigureAwait(false);
        }

        if (menu.Description.Length != 0)
        {
            await output.WriteLineAsync($"Description:\t{menu.Description}").ConfigureAwait(false);
        }

        await output.WriteLineAsync($"Tags:\t{menu.Tags.Format()}").ConfigureAwait(false);
    }

    private static async Task WriteItemTableAsync(TextWriter output, IReadOnlyList<MenuItem> items)
    {
        await output.WriteLineAsync("DRINK_ID\tDISPLAY_NAME\tPRICE\tFEATURED\tAVAILABILITY\tSORT_ORDER")
            .ConfigureAwait(false);
        foreach (MenuItem item in items)
        {
            await output.WriteLineAsync(string.Join('\t',
                item.DrinkId.Value,
                item.DisplayName,
                item.Price?.ToString(),
                item.Featured,
                item.Availability.Value,
                item.SortOrder.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
        }
    }

    private static async Task WriteAnalysisAsync(TextWriter output, MenuAnalysis analysis)
    {
        if (analysis.Items.Count != 0)
        {
            await output.WriteLineAsync().ConfigureAwait(false);
            await output.WriteLineAsync("DRINK_ID\tNAME\tCOST\tPRICE\tMARGIN\tSTATUS").ConfigureAwait(false);
            foreach (MenuItemAnalysis item in analysis.Items)
            {
                string cost = item.Cost is not null && !item.CostUnknown ? item.Cost.Value.ToString() : "n/a";
                string price = item.MenuPrice?.ToString() ??
                    (item.SuggestedPrice is { } suggested ? $"suggested {suggested}" : "n/a");
                string margin = item.Margin is { } value ? FormatPercent(value) : "n/a";
                string status = item.Availability.Value.ToUpperInvariant();
                if (item.Substitutions.Count != 0)
                {
                    AppliedSubstitution substitution = item.Substitutions[0];
                    status += $" (sub: {substitution.SubstituteIngredientId} for {substitution.OriginalIngredientId})";
                }

                await output.WriteLineAsync(string.Join('\t',
                    item.DrinkId.Value, item.Name, cost, price, margin, status)).ConfigureAwait(false);
            }
        }

        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("Analytics:").ConfigureAwait(false);
        await output.WriteLineAsync($"Available:\t{analysis.AvailableCount}/{analysis.TotalCount}").ConfigureAwait(false);
        if (analysis.AverageMargin is { } average)
        {
            await output.WriteLineAsync($"Average margin:\t{FormatPercent(average)}").ConfigureAwait(false);
        }
    }

    private static MenuView ToView(Menu menu) => new(
        menu.Id.Value,
        menu.Revision,
        menu.Name,
        menu.Description,
        menu.Status.Value,
        FormatTime(menu.CreatedAt),
        menu.PublishedAt is { } published ? FormatTime(published) : null,
        menu.Items.Select(ToView).ToArray(),
        menu.Tags.Strings().ToArray());

    private static MenuItemView ToView(MenuItem item) => new(
        item.DrinkId.Value,
        item.DisplayName,
        item.Price?.ToString(),
        item.Featured,
        item.Availability.Value,
        item.SortOrder);

    private static ReadinessReportView ToView(ReadinessReport report) => new(
        report.MenuId.Value,
        report.Status.Value,
        report.Findings.Select(static finding => new ReadinessFindingView(
            finding.Severity.Value,
            finding.Code.Value,
            finding.DrinkId.Value,
            finding.IngredientId?.Value,
            finding.Message)).ToArray());

    private static MenuAnalysisView ToView(MenuAnalysis analysis) => new(
        ToView(analysis.Menu),
        analysis.Items.Select(static item => new MenuItemAnalysisView(
            item.DrinkId.Value,
            item.Name,
            item.Availability.Value,
            item.Substitutions.Select(static substitution => new SubstitutionView(
                substitution.OriginalIngredientId.Value,
                substitution.SubstituteIngredientId.Value,
                substitution.Ratio,
                substitution.QualityImpact.Value)).ToArray(),
            item.Cost?.ToString(),
            item.CostUnknown,
            item.MenuPrice?.ToString(),
            item.Margin,
            item.SuggestedPrice?.ToString())).ToArray(),
        analysis.AvailableCount,
        analysis.TotalCount,
        analysis.AverageMargin);

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    private static string FormatPercent(double value) =>
        $"{(value * 100d).ToString("0", CultureInfo.InvariantCulture)}%";
    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;
    private static Task WriteJsonAsync<T>(TextWriter output, T value) =>
        output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));

    private static async Task WriteFilterHelpAsync(TextWriter output)
    {
        await output.WriteLineAsync("FILTER SYNTAX").ConfigureAwait(false);
        await output.WriteLineAsync("  Comparisons: ==  !=  <  <=  >  >=  in  not in").ConfigureAwait(false);
        await output.WriteLineAsync("  Logic:       && / and   || / or   ! / not   (parentheses)").ConfigureAwait(false);
        await output.WriteLineAsync("FIELDS").ConfigureAwait(false);
        await output.WriteLineAsync("  id           string        Menu ID").ConfigureAwait(false);
        await output.WriteLineAsync("  name         string        Menu name").ConfigureAwait(false);
        await output.WriteLineAsync("  description  string        Menu description").ConfigureAwait(false);
        await output.WriteLineAsync("  status       string        Menu lifecycle status").ConfigureAwait(false);
        await output.WriteLineAsync("  created_at   date          Creation timestamp").ConfigureAwait(false);
        await output.WriteLineAsync("  tags         list<string>  Tags (key or key=value)").ConfigureAwait(false);
        await output.WriteLineAsync("EXAMPLES").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'status == \"published\" && name.contains(\"Summer\")'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'created_at >= date(\"2026-01-01T00:00:00Z\")'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'tags contains \"featured\"'").ConfigureAwait(false);
    }

    private sealed record MenuInput(string? Id, long Revision, string? Name, string? Description);
    private sealed record MenuDrinkInput(MenuId MenuId, DrinkId DrinkId);
    private sealed record MenuView(
        string Id,
        long Revision,
        string Name,
        string Description,
        string Status,
        string CreatedAt,
        string? PublishedAt,
        IReadOnlyList<MenuItemView> Items,
        IReadOnlyList<string> Tags);
    private sealed record MenuItemView(
        string DrinkId,
        string? DisplayName,
        string? Price,
        bool Featured,
        string Availability,
        int SortOrder);
    private sealed record MenuPageView(IReadOnlyList<MenuView> Items, string? Next);
    private sealed record ReadinessReportView(
        string MenuId,
        string Status,
        IReadOnlyList<ReadinessFindingView> Findings);
    private sealed record ReadinessFindingView(
        string Severity,
        string Code,
        string DrinkId,
        string? IngredientId,
        string Message);
    private sealed record MenuAnalysisView(
        MenuView Menu,
        IReadOnlyList<MenuItemAnalysisView> Items,
        int AvailableCount,
        int TotalCount,
        double? AverageMargin);
    private sealed record MenuItemAnalysisView(
        string DrinkId,
        string Name,
        string Availability,
        IReadOnlyList<SubstitutionView> Substitutions,
        string? Cost,
        bool CostUnknown,
        string? MenuPrice,
        double? Margin,
        string? SuggestedPrice);
    private sealed record SubstitutionView(
        string OriginalIngredientId,
        string SubstituteIngredientId,
        double Ratio,
        string QualityImpact);
}
