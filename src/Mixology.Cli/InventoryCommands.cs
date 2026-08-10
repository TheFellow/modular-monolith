using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;

namespace Mixology.Cli;

public interface IInventoryCommandSession : IAsyncDisposable
{
    Task<Page<InventoryStock>> ListAsync(ListInventoryRequest request, CancellationToken cancellationToken);

    Task<InventoryStock> GetAsync(IngredientId ingredientId, CancellationToken cancellationToken);

    Task<InventoryStock> AdjustAsync(AdjustInventoryRequest request, CancellationToken cancellationToken);

    Task<InventoryStock> SetAsync(SetInventoryRequest request, CancellationToken cancellationToken);
}

public sealed class InventoryCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<IInventoryCommandSession>> createSession,
    TextWriter output,
    TextWriter error)
{
    public Func<ParseResult, CancellationToken, ValueTask<IInventoryCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));

    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

    public static InventoryCommandContext FromModule(
        InventoryModule inventory,
        Func<ParseResult, MixologySession> createSession,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(createSession);
        return new InventoryCommandContext(
            (result, _) => ValueTask.FromResult<IInventoryCommandSession>(
                new ModuleCommandSession(inventory, createSession(result))),
            output,
            error);
    }

    private sealed class ModuleCommandSession(InventoryModule inventory, MixologySession session)
        : IInventoryCommandSession
    {
        public Task<Page<InventoryStock>> ListAsync(
            ListInventoryRequest request,
            CancellationToken cancellationToken) =>
            inventory.ListAsync(session, request, cancellationToken);

        public Task<InventoryStock> GetAsync(
            IngredientId ingredientId,
            CancellationToken cancellationToken) =>
            inventory.GetAsync(session, ingredientId, cancellationToken);

        public Task<InventoryStock> AdjustAsync(
            AdjustInventoryRequest request,
            CancellationToken cancellationToken) =>
            inventory.AdjustAsync(session, request, cancellationToken);

        public Task<InventoryStock> SetAsync(
            SetInventoryRequest request,
            CancellationToken cancellationToken) =>
            inventory.SetAsync(session, request, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class InventoryCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(InventoryCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command inventory = new("inventory", "Manage ingredient stock.");
        inventory.Subcommands.Add(BuildList(context));
        inventory.Subcommands.Add(BuildGet(context));
        inventory.Subcommands.Add(BuildAdjust(context));
        inventory.Subcommands.Add(BuildSet(context));
        return inventory;
    }

    private static Command BuildList(InventoryCommandContext context)
    {
        Option<bool> json = JsonOption();
        Option<string?> ingredientId = new("--ingredient-id") { Description = "Filter by ingredient ID" };
        Option<string?> lowStock = new("--low-stock")
        {
            Description = "Show items with quantity less than or equal to the threshold",
        };
        Option<string?> filter = new("--filter") { Description = "Filter expression" };
        Option<bool> filterHelp = new("--filter-help") { Description = "Show filter fields and examples" };
        Option<int> limit = new("--limit") { Description = "Number of entries in a cursor page (default 100)" };
        Option<string?> cursor = new("--cursor") { Description = "Continue after a result cursor" };
        Command command = new("list", "List stock levels.");
        AddOptions(command, json, ingredientId, lowStock, filter, filterHelp, limit, cursor);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(filterHelp))
            {
                await WriteFilterHelpAsync(context.Output).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                Page<InventoryStock> page = await session.ListAsync(
                    new ListInventoryRequest(
                        ParseIngredientId(result.GetValue(ingredientId)),
                        ParseLowStock(result.GetValue(lowStock)),
                        result.GetValue(filter),
                        result.GetValue(cursor),
                        result.GetValue(limit)),
                    cancellationToken).ConfigureAwait(false);
                InventoryView[] items = page.Items.Select(ToView).ToArray();
                if (result.GetValue(json))
                {
                    await WriteJsonAsync(
                        context.Output,
                        new InventoryPageView(items, EmptyToNull(page.Next.Value))).ConfigureAwait(false);
                    return;
                }

                await WriteTableAsync(context.Output, items).ConfigureAwait(false);
                if (!page.Next.IsEmpty)
                {
                    await context.Output.WriteLineAsync($"Next cursor: {page.Next}").ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildGet(InventoryCommandContext context)
    {
        Option<string> ingredientId = RequiredStringOption("--ingredient-id", "Ingredient ID");
        Option<bool> json = JsonOption();
        Command command = new("get", "Get stock for an ingredient.");
        AddOptions(command, ingredientId, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            InventoryStock stock = await session.GetAsync(
                IngredientId.Parse(result.GetRequiredValue(ingredientId)),
                cancellationToken).ConfigureAwait(false);
            InventoryView view = ToView(stock);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, view).ConfigureAwait(false);
                return;
            }

            await WriteDetailAsync(context.Output, view).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildAdjust(InventoryCommandContext context)
    {
        Option<string> ingredientId = RequiredStringOption("--ingredient-id", "Ingredient ID");
        Option<string?> delta = new("--delta", "--amount") { Description = "Quantity adjustment (+/-)" };
        Option<string?> unit = new("--unit", "-u") { Description = UnitUsage() };
        Option<string> reason = RequiredStringOption("--reason", ReasonUsage(), "-r");
        Option<string?> cost = new("--cost-per-unit", "--cost")
        {
            Description = "Cost per unit, for example $1.23 or USD 1.23",
        };
        Option<bool> json = JsonOption();
        Command command = new("adjust", "Patch stock quantity and/or cost.");
        AddOptions(command, ingredientId, delta, unit, reason, cost, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            string? rawDelta = result.GetValue(delta);
            string? rawUnit = result.GetValue(unit);
            Amount? amount = null;
            if (!string.IsNullOrWhiteSpace(rawDelta))
            {
                if (string.IsNullOrWhiteSpace(rawUnit))
                {
                    throw AppError.Invalid("unit is required when delta is provided");
                }

                amount = Amount.Create(ParseDouble(rawDelta, "delta"), Unit.Parse(rawUnit));
            }
            else if (!string.IsNullOrWhiteSpace(rawUnit))
            {
                throw AppError.Invalid("unit requires a delta");
            }

            Price? unitCost = ParseOptionalPrice(result.GetValue(cost));
            InventoryStock adjusted = await session.AdjustAsync(
                new AdjustInventoryRequest(
                    IngredientId.Parse(result.GetRequiredValue(ingredientId)),
                    AdjustmentReason.Parse(result.GetRequiredValue(reason)),
                    amount,
                    unitCost),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, adjusted, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static Command BuildSet(InventoryCommandContext context)
    {
        Option<string> ingredientId = RequiredStringOption("--ingredient-id", "Ingredient ID");
        Option<string> quantity = RequiredStringOption("--quantity", "On-hand quantity", "--amount");
        Option<string> unit = RequiredStringOption("--unit", UnitUsage(), "-u");
        Option<string?> cost = new("--cost-per-unit", "--cost")
        {
            Description = "Cost per unit, for example $1.23 or USD 1.23; existing cost is preserved when omitted",
        };
        Option<bool> json = JsonOption();
        Command command = new("set", "Set the stock quantity.");
        AddOptions(command, ingredientId, quantity, unit, cost, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            IngredientId id = IngredientId.Parse(result.GetRequiredValue(ingredientId));
            Price unitCost = await ResolveSetCostAsync(
                session,
                id,
                result.GetValue(cost),
                cancellationToken).ConfigureAwait(false);
            InventoryStock stock = await session.SetAsync(
                new SetInventoryRequest(
                    id,
                    Amount.Create(
                        ParseDouble(result.GetRequiredValue(quantity), "quantity"),
                        Unit.Parse(result.GetRequiredValue(unit))),
                    unitCost),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, stock, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static async Task<Price> ResolveSetCostAsync(
        IInventoryCommandSession session,
        IngredientId ingredientId,
        string? raw,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return Price.Parse(raw);
        }

        try
        {
            InventoryStock current = await session.GetAsync(ingredientId, cancellationToken).ConfigureAwait(false);
            return current.UnitCost ?? new Price(0m, Currency.Usd);
        }
        catch (Exception exception) when (
            AppError.IsNotFound(exception) && !AppError.IsCancellation(exception))
        {
            return new Price(0m, Currency.Usd);
        }
    }

    private static async Task<int> ExecuteAsync(
        InventoryCommandContext context,
        ParseResult result,
        Func<IInventoryCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IInventoryCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static IngredientId? ParseIngredientId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : IngredientId.Parse(value.Trim());

    private static double? ParseOptionalDouble(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDouble(value, name);

    private static double? ParseLowStock(string? value)
    {
        double? parsed = ParseOptionalDouble(value, "low-stock threshold");
        return parsed >= 0d ? parsed : null;
    }

    private static double ParseDouble(string value, string name) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : throw AppError.Invalid($"invalid {name} \"{value.Trim()}\"");

    private static Price? ParseOptionalPrice(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Price.Parse(value);

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

    private static string UnitUsage() =>
        $"Unit ({string.Join('|', Unit.All.Select(static value => value.Value))})";

    private static string ReasonUsage() =>
        $"Reason ({string.Join('|', AdjustmentReason.All.Select(static value => value.Value))})";

    private static async Task WriteMutationAsync(TextWriter output, InventoryStock stock, bool json)
    {
        if (json)
        {
            await WriteJsonAsync(output, ToView(stock)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(stock.IngredientId.Value).ConfigureAwait(false);
    }

    private static async Task WriteTableAsync(TextWriter output, IReadOnlyList<InventoryView> items)
    {
        await output.WriteLineAsync(
            "ID\tINGREDIENT_ID\tQUANTITY\tRESERVED\tAVAILABLE\tUNIT\tCOST_PER_UNIT\tLAST_UPDATED\tTAGS")
            .ConfigureAwait(false);
        foreach (InventoryView item in items)
        {
            await output.WriteLineAsync(string.Join('\t',
                item.Id,
                item.IngredientId,
                FormatQuantity(item.Quantity),
                FormatQuantity(item.Reserved),
                FormatQuantity(item.Available),
                item.Unit,
                item.CostPerUnit,
                item.LastUpdated,
                string.Join(',', item.Tags))).ConfigureAwait(false);
        }
    }

    private static async Task WriteDetailAsync(TextWriter output, InventoryView item)
    {
        await output.WriteLineAsync($"ID:\t{item.Id}").ConfigureAwait(false);
        await output.WriteLineAsync($"Ingredient ID:\t{item.IngredientId}").ConfigureAwait(false);
        await output.WriteLineAsync($"Quantity:\t{FormatQuantity(item.Quantity)}").ConfigureAwait(false);
        await output.WriteLineAsync($"Reserved:\t{FormatQuantity(item.Reserved)}").ConfigureAwait(false);
        await output.WriteLineAsync($"Available:\t{FormatQuantity(item.Available)}").ConfigureAwait(false);
        await output.WriteLineAsync($"Unit:\t{item.Unit}").ConfigureAwait(false);
        if (!string.IsNullOrEmpty(item.CostPerUnit))
        {
            await output.WriteLineAsync($"Cost per unit:\t{item.CostPerUnit}").ConfigureAwait(false);
        }

        await output.WriteLineAsync($"Last updated:\t{item.LastUpdated}").ConfigureAwait(false);
        await output.WriteLineAsync($"Tags:\t{string.Join(',', item.Tags)}").ConfigureAwait(false);
    }

    private static InventoryView ToView(InventoryStock stock) => new(
        stock.Id.Value,
        stock.IngredientId.Value,
        stock.OnHand.Value,
        stock.Reserved.Value,
        stock.Available.Value,
        stock.OnHand.Unit.Value,
        stock.UnitCost?.ToString(),
        FormatTime(stock.LastUpdated),
        stock.Tags.Strings().ToArray());

    private static string FormatQuantity(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static Task WriteJsonAsync<T>(TextWriter output, T value) =>
        output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static async Task WriteFilterHelpAsync(TextWriter output)
    {
        await output.WriteLineAsync("FILTER SYNTAX").ConfigureAwait(false);
        await output.WriteLineAsync("  Comparisons: ==  !=  <  <=  >  >=  in  not in").ConfigureAwait(false);
        await output.WriteLineAsync("  Logic:       && / and   || / or   ! / not   (parentheses)").ConfigureAwait(false);
        await output.WriteLineAsync("  Strings:     value.contains(\"x\"), startsWith, endsWith, matches")
            .ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("FIELDS").ConfigureAwait(false);
        await output.WriteLineAsync("  id             string        Inventory ID").ConfigureAwait(false);
        await output.WriteLineAsync("  ingredient_id  string        Ingredient ID").ConfigureAwait(false);
        await output.WriteLineAsync("  quantity       number        Quantity on hand").ConfigureAwait(false);
        await output.WriteLineAsync("  unit           string        Measurement unit").ConfigureAwait(false);
        await output.WriteLineAsync("  last_updated   date          Last update timestamp").ConfigureAwait(false);
        await output.WriteLineAsync("  tags           list<string>  Tags (key or key=value)").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("EXAMPLES").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'quantity <= 5 && unit == \"ml\"'").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'ingredient_id.startsWith(\"ing-\") || quantity == 0'")
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'tags contains \"featured\" || tags contains \"region=west\"'")
            .ConfigureAwait(false);
    }

    private sealed record InventoryView(
        string Id,
        string IngredientId,
        double Quantity,
        double Reserved,
        double Available,
        string Unit,
        string? CostPerUnit,
        string LastUpdated,
        IReadOnlyList<string> Tags);

    private sealed record InventoryPageView(IReadOnlyList<InventoryView> Items, string? Next);
}
