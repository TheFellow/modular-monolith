using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class OrdersCliTests
{
    [Fact]
    public void CommandTreeMatchesTheGoOrderSurface()
    {
        Harness harness = new();

        Command command = OrdersCommands.Build(harness.Context);

        Assert.Equal(
            ["place", "list", "get", "complete", "cancel"],
            command.Subcommands.Select(static value => value.Name));
    }

    [Fact]
    public async Task PlaceParsesDirectItemsNotesAndCanonicalJson()
    {
        Harness harness = new();
        MenuId menuId = MenuId.New();
        DrinkId first = DrinkId.New();
        DrinkId second = DrinkId.New();

        int exitCode = await OrdersCommands.Build(harness.Context).Parse(
        [
            "place", "--menu-id", menuId.Value, "--notes", "rush",
            $"{first.Value}:2", $"{second.Value}:1", "--json",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        PlaceOrderRequest request = Assert.IsType<PlaceOrderRequest>(harness.Session.LastPlace);
        Assert.Equal(menuId, request.MenuId);
        Assert.Equal("rush", request.Notes);
        Assert.Collection(
            request.Items,
            item =>
            {
                Assert.Equal(first, item.DrinkId);
                Assert.Equal(2, item.Quantity);
            },
            item =>
            {
                Assert.Equal(second, item.DrinkId);
                Assert.Equal(1, item.Quantity);
            });
        using JsonDocument document = JsonDocument.Parse(harness.Output.ToString());
        Assert.Equal(menuId.Value, document.RootElement.GetProperty("menuId").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("items")[0].GetProperty("quantity").GetInt32());
        Assert.True(harness.Session.Disposed);
    }

    [Fact]
    public async Task PlaceSupportsStdinFileAndTemplateWithoutOpeningTemplateSession()
    {
        MenuId stdinMenu = MenuId.New();
        DrinkId stdinDrink = DrinkId.New();
        Harness stdin = new($$"""
            {"menuId":"{{stdinMenu.Value}}","items":[{"drinkId":"{{stdinDrink.Value}}","quantity":3,"notes":"no garnish"}],"notes":"bar"}
            """);

        int stdinExit = await OrdersCommands.Build(stdin.Context).Parse(["place", "--stdin"]).InvokeAsync();

        Assert.Equal(0, stdinExit);
        Assert.Equal(stdinMenu, stdin.Session.LastPlace?.MenuId);
        Assert.Equal("no garnish", Assert.Single(stdin.Session.LastPlace!.Items).Notes);

        string root = Path.Combine(Path.GetTempPath(), "mixology-orders-cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "order.json");
        MenuId fileMenu = MenuId.New();
        DrinkId fileDrink = DrinkId.New();
        await File.WriteAllTextAsync(
            path,
            $$"""{"menuId":"{{fileMenu.Value}}","items":[{"drinkId":"{{fileDrink.Value}}","quantity":1}]}""");
        try
        {
            Harness file = new();
            int fileExit = await OrdersCommands.Build(file.Context).Parse(
                ["place", "--file", path]).InvokeAsync();
            Assert.Equal(0, fileExit);
            Assert.Equal(fileMenu, file.Session.LastPlace?.MenuId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        Harness template = new();
        int templateExit = await OrdersCommands.Build(template.Context).Parse(
            ["place", "--template"]).InvokeAsync();

        Assert.Equal(0, templateExit);
        Assert.Equal(0, template.SessionCreations);
        using JsonDocument templateDocument = JsonDocument.Parse(template.Output.ToString());
        Assert.Equal("mnu-abc123", templateDocument.RootElement.GetProperty("menuId").GetString());
        Assert.Equal(2, templateDocument.RootElement.GetProperty("items")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task ListMapsStatusMenuFilterAndPagingToCanonicalViews()
    {
        Harness json = new();
        Order order = NewOrder(OrderStatus.Pending);
        string next = OrderId.New().Value;
        string cursor = OrderId.New().Value;
        json.Session.Orders.Add(order);
        json.Session.Page = new Page<Order>([order], next);

        int jsonExit = await OrdersCommands.Build(json.Context).Parse(
        [
            "list", "--status", "pending", "--menu-id", order.MenuId.Value,
            "--filter", "notes.contains(\"rush\")", "--cursor", cursor, "--limit", "2", "--json",
        ]).InvokeAsync();

        Assert.Equal(0, jsonExit);
        ListOrdersRequest request = Assert.IsType<ListOrdersRequest>(json.Session.LastList);
        Assert.Equal(OrderStatus.Pending, request.Status);
        Assert.Equal(order.MenuId, request.MenuId);
        Assert.Equal("notes.contains(\"rush\")", request.Filter);
        Assert.Equal(cursor, request.Cursor.Value);
        Assert.Equal(2, request.Limit);
        using JsonDocument document = JsonDocument.Parse(json.Output.ToString());
        JsonElement row = document.RootElement.GetProperty("items")[0];
        Assert.Equal(order.Id.Value, row.GetProperty("id").GetString());
        Assert.Equal(2, row.GetProperty("items").GetInt32());
        Assert.Equal(3, row.GetProperty("totalQuantity").GetInt32());
        Assert.Equal(next, document.RootElement.GetProperty("next").GetString());

        Harness human = new();
        human.Session.Orders.Add(order);
        human.Session.Page = new Page<Order>([order], next);
        int humanExit = await OrdersCommands.Build(human.Context).Parse(["list"]).InvokeAsync();
        Assert.Equal(0, humanExit);
        Assert.Contains("ID\tMENU_ID\tSTATUS\tITEMS\tTOTAL_QUANTITY", human.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Next cursor:", human.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilterHelpDoesNotOpenASessionAndDocumentsEveryOrderField()
    {
        Harness harness = new();

        int exitCode = await OrdersCommands.Build(harness.Context).Parse(
            ["list", "--filter-help"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(0, harness.SessionCreations);
        Assert.Contains("menu_id", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("created_at", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("notes", harness.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRendersNotesBlockedStateItemsAndIngredientUsage()
    {
        IngredientId blocked = IngredientId.New();
        IngredientId used = IngredientId.New();
        Order order = NewOrder(
            OrderStatus.Blocked,
            [new IngredientUsage(used, "Bourbon", Amount.Create(2d, Unit.Ounce))],
            [blocked]);
        Harness human = WithOrder(order);

        int humanExit = await OrdersCommands.Build(human.Context).Parse(
            ["get", "--id", order.Id.Value]).InvokeAsync();

        Assert.Equal(0, humanExit);
        string output = human.Output.ToString();
        Assert.Contains("Notes:\trush ticket", output, StringComparison.Ordinal);
        Assert.Contains($"Blocked ingredients:\t{blocked.Value}", output, StringComparison.Ordinal);
        Assert.Contains("DRINK_ID\tQUANTITY\tNOTES", output, StringComparison.Ordinal);
        Assert.Contains("INGREDIENT_ID\tNAME\tAMOUNT", output, StringComparison.Ordinal);
        Assert.Contains("2.00 oz", output, StringComparison.Ordinal);

        Harness json = WithOrder(order);
        int jsonExit = await OrdersCommands.Build(json.Context).Parse(
            ["get", "--id", order.Id.Value, "--json"]).InvokeAsync();
        Assert.Equal(0, jsonExit);
        using JsonDocument document = JsonDocument.Parse(json.Output.ToString());
        Assert.Equal(blocked.Value, document.RootElement.GetProperty("blockedIngredients")[0].GetString());
        Assert.Equal("Bourbon", document.RootElement.GetProperty("ingredientUsage")[0].GetProperty("name").GetString());
        Assert.Equal("2.00 oz", document.RootElement.GetProperty("ingredientUsage")[0].GetProperty("amount").GetString());
    }

    [Fact]
    public async Task CompleteAndCancelMapIdsAndRenderIdsOrJson()
    {
        Order pending = NewOrder(OrderStatus.Pending);
        Harness complete = WithOrder(pending);
        int completeExit = await OrdersCommands.Build(complete.Context).Parse(
            ["complete", "--id", pending.Id.Value]).InvokeAsync();

        Assert.Equal(0, completeExit);
        Assert.Equal(pending.Id, complete.Session.LastComplete);
        Assert.Equal(pending.Id.Value, complete.Output.ToString().Trim());

        Harness cancel = WithOrder(pending);
        int cancelExit = await OrdersCommands.Build(cancel.Context).Parse(
            ["cancel", "--id", pending.Id.Value, "--json"]).InvokeAsync();

        Assert.Equal(0, cancelExit);
        Assert.Equal(pending.Id, cancel.Session.LastCancel);
        using JsonDocument document = JsonDocument.Parse(cancel.Output.ToString());
        Assert.Equal("cancelled", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InvalidInputAndWrappedSessionFailuresUseTypedCliErrorsAndDispose()
    {
        Harness invalid = new();
        int invalidExit = await OrdersCommands.Build(invalid.Context).Parse(
            ["place", "--menu-id", MenuId.New().Value, $"{DrinkId.New().Value}:zero"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, invalidExit);
        Assert.Contains("invalid quantity", invalid.Error.ToString(), StringComparison.Ordinal);
        Assert.True(invalid.Session.Disposed);

        Harness denied = new();
        denied.Session.Exception = new InvalidOperationException("wrapped", AppError.Permission("orders denied"));
        int deniedExit = await OrdersCommands.Build(denied.Context).Parse(["list"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitPermission, deniedExit);
        Assert.Equal("orders denied", denied.Error.ToString().Trim());
        Assert.True(denied.Session.Disposed);
    }

    private static Harness WithOrder(Order order)
    {
        Harness harness = new();
        harness.Session.Orders.Add(order);
        return harness;
    }

    private sealed class Harness
    {
        public Harness(string input = "")
        {
            Context = new OrdersCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<IOrdersCommandSession>(Session);
                },
                Output,
                Error,
                new StringReader(input));
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public OrdersCommandContext Context { get; }
        public int SessionCreations { get; private set; }
    }

    private sealed class FakeSession : IOrdersCommandSession
    {
        public List<Order> Orders { get; } = [];
        public Page<Order>? Page { get; set; }
        public ListOrdersRequest? LastList { get; private set; }
        public PlaceOrderRequest? LastPlace { get; private set; }
        public OrderId? LastComplete { get; private set; }
        public OrderId? LastCancel { get; private set; }
        public Exception? Exception { get; set; }
        public bool Disposed { get; private set; }

        public Task<Page<Order>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastList = request;
            return Exception is null
                ? Task.FromResult(Page ?? new Page<Order>(Orders, default))
                : Task.FromException<Page<Order>>(Exception);
        }

        public Task<Order> GetAsync(OrderId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Orders.Single(order => order.Id == id));
        }

        public Task<Order> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPlace = request;
            Order order = new(
                OrderId.New(),
                request.MenuId,
                request.Items.Select(static item => item.Normalize()).ToArray(),
                [],
                [],
                OrderStatus.Pending,
                new DateTimeOffset(2026, 8, 10, 3, 0, 0, TimeSpan.Zero),
                null,
                request.Notes,
                null,
                TagCollection.Empty);
            Orders.Add(order);
            return Task.FromResult(order);
        }

        public Task<Order> CompleteAsync(OrderId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastComplete = id;
            Order current = Orders.Single(order => order.Id == id);
            return Task.FromResult(current with
            {
                Status = OrderStatus.Completed,
                CompletedAt = new DateTimeOffset(2026, 8, 10, 4, 0, 0, TimeSpan.Zero),
            });
        }

        public Task<Order> CancelAsync(OrderId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCancel = id;
            return Task.FromResult(Orders.Single(order => order.Id == id) with
            {
                Status = OrderStatus.Cancelled,
                CompletedAt = null,
            });
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private static Order NewOrder(
        OrderStatus status,
        IReadOnlyList<IngredientUsage>? usage = null,
        IReadOnlyList<IngredientId>? blocked = null) => new(
        OrderId.New(),
        MenuId.New(),
        [
            new OrderItem(DrinkId.New(), 2, "no garnish"),
            new OrderItem(DrinkId.New(), 1, string.Empty),
        ],
        usage ?? [],
        blocked ?? [],
        status,
        new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero),
        status == OrderStatus.Completed
            ? new DateTimeOffset(2026, 8, 10, 2, 0, 0, TimeSpan.Zero)
            : null,
        "rush ticket",
        null,
        TagCollection.Parse("service=bar,priority"));
}
