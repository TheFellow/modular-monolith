using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mixology.Application;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;

namespace Mixology.Cli;

public interface IOrdersCommandSession : IAsyncDisposable
{
    Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken);
    Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken);
    Task<Order> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken);
    Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken);
    Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken);
}

public sealed class OrdersCommandContext(
    Func<ParseResult, CancellationToken, ValueTask<IOrdersCommandSession>> createSession,
    TextWriter output,
    TextWriter error,
    TextReader? input = null)
{
    public Func<ParseResult, CancellationToken, ValueTask<IOrdersCommandSession>> CreateSessionAsync { get; } =
        createSession ?? throw new ArgumentNullException(nameof(createSession));
    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));
    public TextWriter Error { get; } = error ?? throw new ArgumentNullException(nameof(error));
    public TextReader Input { get; } = input ?? Console.In;

    public static OrdersCommandContext FromModule(
        OrdersModule orders,
        Func<ParseResult, MixologySession> createSession,
        TextWriter output,
        TextWriter error,
        TextReader? input = null)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(createSession);
        return new OrdersCommandContext(
            (result, _) => ValueTask.FromResult<IOrdersCommandSession>(
                new ModuleCommandSession(orders, createSession(result))),
            output,
            error,
            input);
    }

    private sealed class ModuleCommandSession(OrdersModule orders, MixologySession session) : IOrdersCommandSession
    {
        public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken) =>
            orders.ListAsync(session, request, cancellationToken);

        public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken) =>
            orders.GetAsync(session, id, cancellationToken);

        public Task<Order> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken) =>
            orders.PlaceAsync(session, request, cancellationToken);

        public Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken) =>
            orders.CompleteAsync(session, id, cancellationToken);

        public Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken) =>
            orders.CancelAsync(session, id, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public static class OrdersCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Build(OrdersCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Command orders = new("orders", "Manage orders.");
        orders.Subcommands.Add(BuildPlace(context));
        orders.Subcommands.Add(BuildList(context));
        orders.Subcommands.Add(BuildGet(context));
        orders.Subcommands.Add(BuildComplete(context));
        orders.Subcommands.Add(BuildCancel(context));
        return orders;
    }

    private static Command BuildPlace(OrdersCommandContext context)
    {
        Argument<string[]> items = new("items")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Drink quantities as <drink-id>:<quantity>",
        };
        Option<string?> menuId = new("--menu-id") { Description = "Menu ID" };
        Option<string?> notes = new("--notes") { Description = "Order notes" };
        Option<bool> json = JsonOption();
        Option<bool> template = TemplateOption();
        Option<bool> stdin = StdinOption();
        Option<string?> file = FileOption();
        Command command = new("place", "Place an order.");
        command.Arguments.Add(items);
        AddOptions(command, menuId, notes, json, template, stdin, file);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(template))
            {
                await WriteJsonAsync(context.Output, new OrderInput(
                    "mnu-abc123",
                    [
                        new OrderItemInput("drk-abc123", 2, null),
                        new OrderItemInput("drk-def456", 1, null),
                    ],
                    null)).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                PlaceOrderRequest request;
                if (UsesStructuredInput(result, stdin, file))
                {
                    OrderInput input = await ReadInputAsync(
                        context,
                        result,
                        stdin,
                        file,
                        cancellationToken).ConfigureAwait(false);
                    request = ToRequest(input);
                }
                else
                {
                    string rawMenuId = result.GetValue(menuId)
                        ?? throw AppError.Invalid("menu-id is required (or use --stdin/--file)");
                    string[] specs = result.GetValue(items) ?? [];
                    if (specs.Length == 0)
                    {
                        throw AppError.Invalid("items are required (or use --stdin/--file)");
                    }

                    request = new PlaceOrderRequest(
                        MenuId.Parse(rawMenuId),
                        specs.Select(ParseItem).ToArray(),
                        result.GetValue(notes) ?? string.Empty);
                }

                Order placed = await session.PlaceAsync(request, cancellationToken).ConfigureAwait(false);
                await WriteMutationAsync(context.Output, placed, result.GetValue(json)).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildList(OrdersCommandContext context)
    {
        Option<bool> json = JsonOption();
        Option<string?> status = new("--status")
        {
            Description = "Status (pending|blocked|completed|cancelled)",
        };
        Option<string?> menuId = new("--menu-id") { Description = "Filter by menu ID" };
        Option<string?> filter = new("--filter") { Description = "Filter expression" };
        Option<bool> filterHelp = new("--filter-help") { Description = "Show filter fields and examples" };
        Option<int> limit = new("--limit") { Description = "Number of entries in a cursor page (default 100)" };
        Option<string?> cursor = new("--cursor") { Description = "Continue after a result cursor" };
        Command command = new("list", "List orders.");
        AddOptions(command, json, status, menuId, filter, filterHelp, limit, cursor);
        command.SetAction(async (result, cancellationToken) =>
        {
            if (result.GetValue(filterHelp))
            {
                await WriteFilterHelpAsync(context.Output).ConfigureAwait(false);
                return 0;
            }

            return await ExecuteAsync(context, result, async session =>
            {
                Page<Order> page = await session.ListAsync(
                    new ListOrdersRequest(
                        ParseStatus(result.GetValue(status)),
                        ParseMenuId(result.GetValue(menuId)),
                        result.GetValue(filter),
                        result.GetValue(cursor),
                        result.GetValue(limit)),
                    cancellationToken).ConfigureAwait(false);
                if (result.GetValue(json))
                {
                    await WriteJsonAsync(
                        context.Output,
                        new OrderPageView(page.Items.Select(ToRowView).ToArray(), EmptyToNull(page.Next.Value)))
                        .ConfigureAwait(false);
                    return;
                }

                await WriteOrderTableAsync(context.Output, page.Items).ConfigureAwait(false);
                if (!page.Next.IsEmpty)
                {
                    await context.Output.WriteLineAsync($"Next cursor: {page.Next}").ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildGet(OrdersCommandContext context)
    {
        Option<string> id = RequiredStringOption("--id", "Order ID");
        Option<bool> json = JsonOption();
        Command command = new("get", "Get an order.");
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Order order = await session.GetAsync(
                OrderId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            if (result.GetValue(json))
            {
                await WriteJsonAsync(context.Output, ToView(order)).ConfigureAwait(false);
                return;
            }

            await WriteOrderDetailAsync(context.Output, order).ConfigureAwait(false);
            await context.Output.WriteLineAsync().ConfigureAwait(false);
            await WriteItemTableAsync(context.Output, order.Items).ConfigureAwait(false);
            if (order.IngredientUsage.Count != 0)
            {
                await context.Output.WriteLineAsync().ConfigureAwait(false);
                await WriteIngredientUsageTableAsync(context.Output, order.IngredientUsage).ConfigureAwait(false);
            }
        }, cancellationToken));
        return command;
    }

    private static Command BuildComplete(OrdersCommandContext context) =>
        BuildMutation(
            "complete",
            "Complete an order.",
            context,
            static (session, id, token) => session.CompleteAsync(id, token));

    private static Command BuildCancel(OrdersCommandContext context) =>
        BuildMutation(
            "cancel",
            "Cancel an order.",
            context,
            static (session, id, token) => session.CancelAsync(id, token));

    private static Command BuildMutation(
        string name,
        string description,
        OrdersCommandContext context,
        Func<IOrdersCommandSession, OrderId, CancellationToken, Task<Order>> mutation)
    {
        Option<string> id = RequiredStringOption("--id", "Order ID");
        Option<bool> json = JsonOption();
        Command command = new(name, description);
        AddOptions(command, id, json);
        command.SetAction((result, cancellationToken) => ExecuteAsync(context, result, async session =>
        {
            Order order = await mutation(
                session,
                OrderId.Parse(result.GetRequiredValue(id)),
                cancellationToken).ConfigureAwait(false);
            await WriteMutationAsync(context.Output, order, result.GetValue(json)).ConfigureAwait(false);
        }, cancellationToken));
        return command;
    }

    private static PlaceOrderItem ParseItem(string specification)
    {
        int separator = specification.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == specification.Length - 1)
        {
            throw AppError.Invalid($"invalid item \"{specification}\" (expected drink-id:quantity)");
        }

        string rawQuantity = specification[(separator + 1)..];
        if (!int.TryParse(rawQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out int quantity) || quantity <= 0)
        {
            throw AppError.Invalid($"invalid quantity in \"{specification}\"");
        }

        return new PlaceOrderItem(DrinkId.Parse(specification[..separator]), quantity);
    }

    private static PlaceOrderRequest ToRequest(OrderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.MenuId))
        {
            throw AppError.Invalid("menu_id is required");
        }

        OrderItemInput[] items = input.Items?.ToArray() ?? [];
        if (items.Length == 0)
        {
            throw AppError.Invalid("items are required");
        }

        return new PlaceOrderRequest(
            MenuId.Parse(input.MenuId),
            items.Select(static (item, index) =>
            {
                if (string.IsNullOrWhiteSpace(item.DrinkId))
                {
                    throw AppError.Invalid($"item {index}: drink_id is required");
                }

                if (item.Quantity <= 0)
                {
                    throw AppError.Invalid($"item {index}: quantity must be greater than zero");
                }

                return new PlaceOrderItem(
                    DrinkId.Parse(item.DrinkId),
                    item.Quantity,
                    item.Notes ?? string.Empty);
            }).ToArray(),
            input.Notes ?? string.Empty);
    }

    private static bool UsesStructuredInput(ParseResult result, Option<bool> stdin, Option<string?> file) =>
        result.GetValue(stdin) || !string.IsNullOrWhiteSpace(result.GetValue(file));

    private static async Task<OrderInput> ReadInputAsync(
        OrdersCommandContext context,
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
            return JsonSerializer.Deserialize<OrderInput>(json, JsonOptions)
                ?? throw AppError.Invalid("parse order json: input is empty");
        }
        catch (JsonException exception)
        {
            throw AppError.Invalid("parse order json", exception);
        }
    }

    private static async Task<int> ExecuteAsync(
        OrdersCommandContext context,
        ParseResult result,
        Func<IOrdersCommandSession, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IOrdersCommandSession session = await context.CreateSessionAsync(result, cancellationToken)
                .ConfigureAwait(false);
            await action(session).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            return await CliErrorAdapter.WriteAsync(context.Error, exception).ConfigureAwait(false);
        }
    }

    private static async Task WriteMutationAsync(TextWriter output, Order order, bool json)
    {
        if (json)
        {
            await WriteJsonAsync(output, ToView(order)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(order.Id.Value).ConfigureAwait(false);
    }

    private static async Task WriteOrderTableAsync(TextWriter output, IReadOnlyList<Order> orders)
    {
        await output.WriteLineAsync(
            "ID\tMENU_ID\tSTATUS\tITEMS\tTOTAL_QUANTITY\tCREATED_AT\tCOMPLETED_AT\tTAGS").ConfigureAwait(false);
        foreach (Order order in orders)
        {
            await output.WriteLineAsync(string.Join('\t',
                order.Id.Value,
                order.MenuId.Value,
                order.Status.Value,
                order.Items.Count.ToString(CultureInfo.InvariantCulture),
                order.Items.Sum(static item => item.Quantity).ToString(CultureInfo.InvariantCulture),
                FormatTime(order.CreatedAt),
                order.CompletedAt is { } completed ? FormatTime(completed) : string.Empty,
                order.Tags.Format())).ConfigureAwait(false);
        }
    }

    private static async Task WriteOrderDetailAsync(TextWriter output, Order order)
    {
        await output.WriteLineAsync($"ID:\t{order.Id}").ConfigureAwait(false);
        await output.WriteLineAsync($"Menu ID:\t{order.MenuId}").ConfigureAwait(false);
        await output.WriteLineAsync($"Status:\t{order.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Created at:\t{FormatTime(order.CreatedAt)}").ConfigureAwait(false);
        if (order.CompletedAt is { } completed)
        {
            await output.WriteLineAsync($"Completed at:\t{FormatTime(completed)}").ConfigureAwait(false);
        }

        if (order.Notes.Length != 0)
        {
            await output.WriteLineAsync($"Notes:\t{order.Notes}").ConfigureAwait(false);
        }

        if (order.BlockedIngredientIds.Count != 0)
        {
            await output.WriteLineAsync(
                $"Blocked ingredients:\t{string.Join(", ", order.BlockedIngredientIds)}").ConfigureAwait(false);
        }

        await output.WriteLineAsync($"Tags:\t{order.Tags.Format()}").ConfigureAwait(false);
    }

    private static async Task WriteItemTableAsync(TextWriter output, IReadOnlyList<OrderItem> items)
    {
        await output.WriteLineAsync("DRINK_ID\tQUANTITY\tNOTES").ConfigureAwait(false);
        foreach (OrderItem item in items)
        {
            await output.WriteLineAsync(string.Join('\t',
                item.DrinkId.Value,
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                item.Notes)).ConfigureAwait(false);
        }
    }

    private static async Task WriteIngredientUsageTableAsync(
        TextWriter output,
        IReadOnlyList<IngredientUsage> usage)
    {
        await output.WriteLineAsync("INGREDIENT_ID\tNAME\tAMOUNT").ConfigureAwait(false);
        foreach (IngredientUsage item in usage)
        {
            await output.WriteLineAsync(string.Join('\t',
                item.IngredientId.Value,
                item.Name,
                item.Amount.ToString())).ConfigureAwait(false);
        }
    }

    private static OrderRowView ToRowView(Order order) => new(
        order.Id.Value,
        order.MenuId.Value,
        order.Status.Value,
        order.Items.Count,
        order.Items.Sum(static item => item.Quantity),
        FormatTime(order.CreatedAt),
        order.CompletedAt is { } completed ? FormatTime(completed) : null,
        order.Tags.Strings());

    private static OrderView ToView(Order order) => new(
        order.Id.Value,
        order.MenuId.Value,
        order.Status.Value,
        FormatTime(order.CreatedAt),
        order.CompletedAt is { } completed ? FormatTime(completed) : null,
        EmptyToNull(order.Notes),
        order.Tags.Strings(),
        order.BlockedIngredientIds.Select(static id => id.Value).ToArray(),
        order.Items.Select(static item => new OrderItemView(
            item.DrinkId.Value,
            item.Quantity,
            EmptyToNull(item.Notes))).ToArray(),
        order.IngredientUsage.Select(static item => new IngredientUsageView(
            item.IngredientId.Value,
            item.Name,
            item.Amount.ToString())).ToArray());

    private static OrderStatus? ParseStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : OrderStatus.Parse(value);

    private static MenuId? ParseMenuId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : MenuId.Parse(value);

    private static Option<string> RequiredStringOption(string name, string description) =>
        new(name) { Description = description, Required = true };

    private static Option<bool> JsonOption() => new("--json") { Description = "Output JSON" };
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

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static Task WriteJsonAsync<T>(TextWriter output, T value) =>
        output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));

    private static async Task WriteFilterHelpAsync(TextWriter output)
    {
        await output.WriteLineAsync("FILTER SYNTAX").ConfigureAwait(false);
        await output.WriteLineAsync("  Comparisons: ==  !=  <  <=  >  >=  in  not in").ConfigureAwait(false);
        await output.WriteLineAsync("  Logic:       && / and   || / or   ! / not   (parentheses)").ConfigureAwait(false);
        await output.WriteLineAsync("FIELDS").ConfigureAwait(false);
        await output.WriteLineAsync("  id           string        Order ID").ConfigureAwait(false);
        await output.WriteLineAsync("  menu_id      string        Menu ID").ConfigureAwait(false);
        await output.WriteLineAsync("  status       string        Order status").ConfigureAwait(false);
        await output.WriteLineAsync("  created_at   date          Creation timestamp").ConfigureAwait(false);
        await output.WriteLineAsync("  notes        string        Order notes").ConfigureAwait(false);
        await output.WriteLineAsync("  tags         list<string>  Tags (key or key=value)").ConfigureAwait(false);
        await output.WriteLineAsync("EXAMPLES").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'status in [\"pending\", \"completed\"]'").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'menu_id.startsWith(\"mnu-\")'").ConfigureAwait(false);
        await output.WriteLineAsync("  --filter 'notes.contains(\"rush\")'").ConfigureAwait(false);
    }

    private sealed record OrderInput(
        string? MenuId,
        IReadOnlyList<OrderItemInput>? Items,
        string? Notes);
    private sealed record OrderItemInput(string? DrinkId, int Quantity, string? Notes);
    private sealed record OrderRowView(
        string Id,
        string MenuId,
        string Status,
        int Items,
        int TotalQuantity,
        string CreatedAt,
        string? CompletedAt,
        IReadOnlyList<string> Tags);
    private sealed record OrderPageView(IReadOnlyList<OrderRowView> Items, string? Next);
    private sealed record OrderView(
        string Id,
        string MenuId,
        string Status,
        string CreatedAt,
        string? CompletedAt,
        string? Notes,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> BlockedIngredients,
        IReadOnlyList<OrderItemView> Items,
        IReadOnlyList<IngredientUsageView> IngredientUsage);
    private sealed record OrderItemView(string DrinkId, int Quantity, string? Notes);
    private sealed record IngredientUsageView(string IngredientId, string Name, string Amount);
}
