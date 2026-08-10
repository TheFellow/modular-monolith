using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class InventoryCliTests
{
    [Fact]
    public void CommandTreeExposesListGetAdjustAndSet()
    {
        Harness harness = new();

        Command command = InventoryCommands.Build(harness.Context);

        Assert.Equal(["list", "get", "adjust", "set"], command.Subcommands.Select(static value => value.Name));
    }

    [Fact]
    public async Task ListMapsStructuredOptionsAndWritesCanonicalJson()
    {
        Harness harness = new();
        InventoryStock stock = harness.AddStock(12.5, 2.5, new Price(1.25m, Currency.Usd));
        string cursor = InventoryId.New().Value;
        harness.Session.Page = new Page<InventoryStock>([stock], new Cursor(InventoryId.New().Value));

        int exitCode = await InventoryCommands.Build(harness.Context).Parse(
        [
            "list",
            "--ingredient-id", stock.IngredientId.Value,
            "--low-stock", "15.5",
            "--filter", "quantity <= 15.5 && unit == \"oz\"",
            "--cursor", cursor,
            "--limit", "2",
            "--json",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(harness.Error.ToString());
        ListInventoryRequest request = Assert.IsType<ListInventoryRequest>(harness.Session.LastList);
        Assert.Equal(stock.IngredientId, request.IngredientId);
        Assert.Equal(15.5, request.LowStock);
        Assert.Equal("quantity <= 15.5 && unit == \"oz\"", request.Filter);
        Assert.Equal(cursor, request.Cursor.Value);
        Assert.Equal(2, request.Limit);
        Assert.True(harness.Session.Disposed);

        using JsonDocument document = JsonDocument.Parse(harness.Output.ToString());
        JsonElement root = document.RootElement;
        JsonElement item = root.GetProperty("items")[0];
        Assert.Equal(stock.Id.Value, item.GetProperty("id").GetString());
        Assert.Equal(stock.IngredientId.Value, item.GetProperty("ingredientId").GetString());
        Assert.Equal(12.5, item.GetProperty("quantity").GetDouble());
        Assert.Equal(2.5, item.GetProperty("reserved").GetDouble());
        Assert.Equal(10, item.GetProperty("available").GetDouble());
        Assert.Equal("$1.25", item.GetProperty("costPerUnit").GetString());
        Assert.Equal(harness.Session.Page.Next.Value, root.GetProperty("next").GetString());
    }

    [Fact]
    public async Task ListAndGetHumanOutputUseStableInventoryColumns()
    {
        Harness list = new();
        InventoryStock stock = list.AddStock(3, 1, null);
        list.Session.Page = new Page<InventoryStock>([stock], new Cursor(InventoryId.New().Value));

        int listExit = await InventoryCommands.Build(list.Context).Parse(["list"]).InvokeAsync();

        Assert.Equal(0, listExit);
        Assert.Contains(
            "ID\tINGREDIENT_ID\tQUANTITY\tRESERVED\tAVAILABLE\tUNIT\tCOST_PER_UNIT\tLAST_UPDATED\tTAGS",
            list.Output.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("3.00\t1.00\t2.00\toz", list.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Next cursor:", list.Output.ToString(), StringComparison.Ordinal);

        Harness get = new();
        get.Session.Items.Add(stock);
        int getExit = await InventoryCommands.Build(get.Context).Parse(
            ["get", "--ingredient-id", stock.IngredientId.Value]).InvokeAsync();

        Assert.Equal(0, getExit);
        Assert.Contains($"Ingredient ID:\t{stock.IngredientId}", get.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Available:\t2.00", get.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilterHelpDoesNotOpenACommandSession()
    {
        Harness harness = new();

        int exitCode = await InventoryCommands.Build(harness.Context)
            .Parse(["list", "--filter-help"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(0, harness.SessionCreations);
        Assert.Contains("ingredient_id", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("last_updated", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("quantity <= 5", harness.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegativeLowStockSentinelLeavesTheOptionalFilterUnset()
    {
        Harness harness = new();

        int exitCode = await InventoryCommands.Build(harness.Context).Parse(
            ["list", "--low-stock", "-1"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Null(harness.Session.LastList?.LowStock);
    }

    [Fact]
    public async Task AdjustMapsAmountUnitCostAndReasonAndWritesTheIngredientId()
    {
        Harness harness = new();
        IngredientId id = IngredientId.New();

        int exitCode = await InventoryCommands.Build(harness.Context).Parse(
        [
            "adjust",
            "--ingredient-id", id.Value,
            "--delta", "-2.25",
            "--unit", "oz",
            "--cost-per-unit", "USD 1.50",
            "--reason", "used",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        AdjustInventoryRequest request = Assert.IsType<AdjustInventoryRequest>(harness.Session.LastAdjust);
        Assert.Equal(id, request.IngredientId);
        Assert.Equal(-2.25, request.Delta?.Value);
        Assert.Equal(Unit.Ounce, request.Delta?.Unit);
        Assert.Equal(new Price(1.5m, Currency.Usd), request.UnitCost);
        Assert.Equal(AdjustmentReason.Used, request.Reason);
        Assert.Equal(id.Value, harness.Output.ToString().Trim());
    }

    [Fact]
    public async Task CostOnlyAdjustmentAndJsonMutationAreSupported()
    {
        Harness harness = new();
        IngredientId id = IngredientId.New();

        int exitCode = await InventoryCommands.Build(harness.Context).Parse(
        [
            "adjust",
            "--ingredient-id", id.Value,
            "--cost", "$2.00",
            "--reason", "corrected",
            "--json",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Null(harness.Session.LastAdjust?.Delta);
        Assert.Equal(new Price(2m, Currency.Usd), harness.Session.LastAdjust?.UnitCost);
        using JsonDocument document = JsonDocument.Parse(harness.Output.ToString());
        Assert.Equal(id.Value, document.RootElement.GetProperty("ingredientId").GetString());
        Assert.Equal("$2.00", document.RootElement.GetProperty("costPerUnit").GetString());
    }

    [Fact]
    public async Task SetMapsQuantityUnitAndExplicitOrPreservedCost()
    {
        Harness explicitCost = new();
        IngredientId newId = IngredientId.New();
        int explicitExit = await InventoryCommands.Build(explicitCost.Context).Parse(
        [
            "set",
            "--ingredient-id", newId.Value,
            "--quantity", "12.75",
            "--unit", "ml",
            "--cost-per-unit", "EUR 0.25",
        ]).InvokeAsync();

        Assert.Equal(0, explicitExit);
        Assert.Equal(12.75, explicitCost.Session.LastSet?.OnHand.Value);
        Assert.Equal(Unit.Milliliter, explicitCost.Session.LastSet?.OnHand.Unit);
        Assert.Equal(new Price(0.25m, Currency.Eur), explicitCost.Session.LastSet?.UnitCost);
        Assert.Equal(newId.Value, explicitCost.Output.ToString().Trim());

        Harness preserve = new();
        InventoryStock current = preserve.AddStock(5, 0, new Price(3m, Currency.Usd));
        int preserveExit = await InventoryCommands.Build(preserve.Context).Parse(
        [
            "set",
            "--ingredient-id", current.IngredientId.Value,
            "--amount", "8",
            "--unit", "oz",
        ]).InvokeAsync();

        Assert.Equal(0, preserveExit);
        Assert.Equal(current.UnitCost, preserve.Session.LastSet?.UnitCost);

        Harness missing = new();
        missing.Session.GetException = AppError.Internal(
            "wrapped lookup",
            AppError.NotFound("stock not found"));
        IngredientId missingId = IngredientId.New();
        int missingExit = await InventoryCommands.Build(missing.Context).Parse(
        [
            "set",
            "--ingredient-id", missingId.Value,
            "--quantity", "1",
            "--unit", "piece",
        ]).InvokeAsync();

        Assert.Equal(0, missingExit);
        Assert.Equal(new Price(0m, Currency.Usd), missing.Session.LastSet?.UnitCost);
    }

    [Fact]
    public async Task InvalidValuesAndSessionFailuresUseTypedErrorsAndDispose()
    {
        Harness invalid = new();
        int invalidExit = await InventoryCommands.Build(invalid.Context).Parse(
        [
            "adjust",
            "--ingredient-id", IngredientId.New().Value,
            "--delta", "nope",
            "--unit", "oz",
            "--reason", "used",
        ]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, invalidExit);
        Assert.Contains("invalid delta", invalid.Error.ToString(), StringComparison.Ordinal);
        Assert.True(invalid.Session.Disposed);

        Harness denied = new();
        denied.Session.Exception = AppError.Permission("inventory denied");
        int deniedExit = await InventoryCommands.Build(denied.Context).Parse(["list"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitPermission, deniedExit);
        Assert.Equal("inventory denied", denied.Error.ToString().Trim());
        Assert.True(denied.Session.Disposed);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Context = new InventoryCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<IInventoryCommandSession>(Session);
                },
                Output,
                Error);
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public InventoryCommandContext Context { get; }
        public int SessionCreations { get; private set; }

        public InventoryStock AddStock(double quantity, double reserved, Price? cost)
        {
            InventoryStock stock = NewStock(IngredientId.New(), quantity, reserved, cost);
            Session.Items.Add(stock);
            return stock;
        }
    }

    private sealed class FakeSession : IInventoryCommandSession
    {
        public List<InventoryStock> Items { get; } = [];
        public Page<InventoryStock> Page { get; set; } = new([], default);
        public ListInventoryRequest? LastList { get; private set; }
        public AdjustInventoryRequest? LastAdjust { get; private set; }
        public SetInventoryRequest? LastSet { get; private set; }
        public Exception? Exception { get; set; }
        public Exception? GetException { get; set; }
        public bool Disposed { get; private set; }

        public Task<Page<InventoryStock>> ListAsync(
            ListInventoryRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastList = request;
            return Exception is null
                ? Task.FromResult(Page.Items.Count == 0 ? new Page<InventoryStock>(Items, Page.Next) : Page)
                : Task.FromException<Page<InventoryStock>>(Exception);
        }

        public Task<InventoryStock> GetAsync(IngredientId ingredientId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GetException is not null)
            {
                return Task.FromException<InventoryStock>(GetException);
            }

            InventoryStock? stock = Items.SingleOrDefault(value => value.IngredientId == ingredientId);
            return stock is null
                ? Task.FromException<InventoryStock>(AppError.NotFound($"stock for ingredient {ingredientId} not found"))
                : Task.FromResult(stock);
        }

        public Task<InventoryStock> AdjustAsync(
            AdjustInventoryRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastAdjust = request;
            InventoryStock stock = NewStock(
                request.IngredientId,
                request.Delta?.Value ?? 0,
                0,
                request.UnitCost);
            return Task.FromResult(stock);
        }

        public Task<InventoryStock> SetAsync(SetInventoryRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSet = request;
            return Task.FromResult(NewStock(
                request.IngredientId,
                request.OnHand.Value,
                0,
                request.UnitCost,
                request.OnHand.Unit));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private static InventoryStock NewStock(
        IngredientId ingredientId,
        double quantity,
        double reserved,
        Price? cost,
        Unit? unit = null) => new(
        InventoryId.New(),
        ingredientId,
        Amount.Create(quantity, unit ?? Unit.Ounce),
        Amount.Create(reserved, unit ?? Unit.Ounce),
        cost,
        new DateTimeOffset(2026, 8, 9, 23, 30, 0, TimeSpan.Zero),
        TagCollection.Empty);
}
