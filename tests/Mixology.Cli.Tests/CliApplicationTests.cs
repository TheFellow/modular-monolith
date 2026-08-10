using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void Build_RegistersStatusCommand()
    {
        RootCommand root = CliApplication.Build();

        Command status = Assert.Single(root.Subcommands, command => command.Name == "status");
        Assert.Equal("Show the application dashboard aggregate.", status.Description);
        Assert.Contains(status.Options, option => option.Name == "--json");
    }

    [Fact]
    public async Task StatusInitializesTheConfiguredDatabaseWithoutWritingDiagnostics()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-tests", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter output = new();
        StringWriter error = new();

        try
        {
            int exitCode = await CliApplication.Build(output, error)
                .Parse(["--db", database, "--actor", "owner", "status"])
                .InvokeAsync();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(database));
            Assert.Contains("DRINKS\tINGREDIENTS\tINVENTORY", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("RECENT ACTIVITY", output.ToString(), StringComparison.Ordinal);
            Assert.Empty(error.ToString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StatusJsonHonorsAsAliasAndUsesCanonicalAuditActor()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-tests", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter createOutput = new();
        StringWriter createError = new();
        StringWriter statusOutput = new();
        StringWriter statusError = new();

        try
        {
            int createExit = await CliApplication.Build(createOutput, createError)
                .Parse([
                    "--db", database,
                    "--as", "manager",
                    "ingredients", "create", "Dashboard Gin",
                    "--category", "spirit",
                    "--unit", "oz",
                ])
                .InvokeAsync();
            int statusExit = await CliApplication.Build(statusOutput, statusError)
                .Parse(["--db", database, "--as", "owner", "status", "--json"])
                .InvokeAsync();

            Assert.Equal(0, createExit);
            Assert.Equal(0, statusExit);
            Assert.Empty(createError.ToString());
            Assert.Empty(statusError.ToString());
            using JsonDocument document = JsonDocument.Parse(statusOutput.ToString());
            Assert.Equal(1, document.RootElement.GetProperty("ingredientCount").GetInt32());
            JsonElement activity = Assert.Single(document.RootElement.GetProperty("recentActivity").EnumerateArray());
            Assert.Equal("Mixology::Actor::\"manager\"", activity.GetProperty("actor").GetString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TypedInvalidInputUsesTheSharedExitCodeAndSafeMessage()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliApplication.Build(output, error)
            .Parse(["--actor", "visitor", "status"])
            .InvokeAsync();

        Assert.Equal(10, exitCode);
        Assert.Empty(output.ToString());
        Assert.Equal("unknown actor: \"visitor\"", error.ToString().Trim());
    }

    [Fact]
    public async Task ErrorAdapterPreservesTypedMappingsAndHidesInternalDetail()
    {
        StringWriter invalidOutput = new();
        StringWriter internalOutput = new();

        int invalid = await CliErrorAdapter.WriteAsync(invalidOutput, AppError.Invalid("name is required"));
        int internalCode = await CliErrorAdapter.WriteAsync(
            internalOutput,
            AppError.Internal("database password leaked"));

        Assert.Equal(ErrorCatalog.ExitInvalid, invalid);
        Assert.Equal("name is required", invalidOutput.ToString().Trim());
        Assert.Equal(ErrorCatalog.ExitInternal, internalCode);
        Assert.Equal("internal error", internalOutput.ToString().Trim());
    }

    [Fact]
    public async Task CancellationAndUnknownFailuresRemainNonApplicationOutcomes()
    {
        StringWriter cancellationOutput = new();
        StringWriter unknownOutput = new();

        int cancellation = await CliErrorAdapter.WriteAsync(
            cancellationOutput,
            new InvalidOperationException("outer", new TaskCanceledException()));
        int unknown = await CliErrorAdapter.WriteAsync(
            unknownOutput,
            new IOException("secret path"));

        Assert.Equal(ErrorCatalog.ExitGeneral, cancellation);
        Assert.Equal("operation cancelled", cancellationOutput.ToString().Trim());
        Assert.Equal(ErrorCatalog.ExitGeneral, unknown);
        Assert.Equal("internal error", unknownOutput.ToString().Trim());
    }

    [Fact]
    public async Task IngredientCommandsPersistAcrossIndependentCliInvocations()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-ingredients", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter createOutput = new();
        StringWriter createError = new();
        StringWriter listOutput = new();
        StringWriter listError = new();

        try
        {
            int created = await CliApplication.Build(createOutput, createError).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "ingredients", "create", "House Gin",
                "--category", "spirit",
                "--unit", "oz",
                "--description", "Dry",
            ]).InvokeAsync();
            int listed = await CliApplication.Build(listOutput, listError).Parse(
            [
                "--db", database,
                "--actor", "anonymous",
                "ingredients", "list",
                "--json",
            ]).InvokeAsync();

            Assert.Equal(0, created);
            Assert.Equal(0, listed);
            Assert.StartsWith("ing-", createOutput.ToString().Trim(), StringComparison.Ordinal);
            Assert.Empty(createError.ToString());
            Assert.Empty(listError.ToString());
            using JsonDocument document = JsonDocument.Parse(listOutput.ToString());
            JsonElement item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("House Gin", item.GetProperty("name").GetString());
            Assert.Equal("spirit", item.GetProperty("category").GetString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CatalogMenuAndOrderPersistAcrossIndependentCliInvocations()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-catalog", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter ingredientOutput = new();
        StringWriter inventoryOutput = new();
        StringWriter drinkOutput = new();
        StringWriter listOutput = new();
        StringWriter menuOutput = new();
        StringWriter menuShowOutput = new();
        StringWriter orderOutput = new();
        StringWriter orderGetOutput = new();
        StringWriter reservedInventoryOutput = new();
        StringWriter releasedInventoryOutput = new();
        StringWriter completedInventoryOutput = new();
        StringWriter secondOrderOutput = new();
        StringWriter tagOutput = new();
        StringWriter error = new();

        try
        {
            int ingredientExit = await CliApplication.Build(ingredientOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "ingredients", "create", "CLI Gin",
                "--category", "spirit",
                "--unit", "oz",
            ]).InvokeAsync();
            string ingredientId = ingredientOutput.ToString().Trim();
            int inventoryExit = await CliApplication.Build(inventoryOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "inventory", "set",
                "--ingredient-id", ingredientId,
                "--quantity", "12",
                "--unit", "oz",
                "--cost-per-unit", "USD 1.25",
            ]).InvokeAsync();
            string drinkJson = $$"""
                {
                  "name": "CLI Gimlet",
                  "category": "cocktail",
                  "glass": "coupe",
                  "recipe": {
                    "ingredients": [{
                      "ingredient_id": "{{ingredientId}}",
                      "amount": 2,
                      "unit": "oz"
                    }],
                    "steps": ["Stir"]
                  }
                }
                """;
            int drinkExit = await CliApplication.Build(
                drinkOutput,
                error,
                new StringReader(drinkJson)).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "drinks", "create", "--stdin",
            ]).InvokeAsync();
            string drinkId = drinkOutput.ToString().Trim();
            int menuExit = await CliApplication.Build(menuOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "menus", "create", "CLI Menu",
            ]).InvokeAsync();
            string menuId = menuOutput.ToString().Trim();
            int addDrinkExit = await CliApplication.Build(TextWriter.Null, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "menus", "add-drink",
                "--menu-id", menuId,
                "--drink-id", drinkId,
            ]).InvokeAsync();
            int publishExit = await CliApplication.Build(TextWriter.Null, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "menus", "publish", "--id", menuId,
            ]).InvokeAsync();
            int showMenuExit = await CliApplication.Build(menuShowOutput, error).Parse(
            [
                "--db", database,
                "--actor", "anonymous",
                "menus", "show", "--id", menuId, "--json",
            ]).InvokeAsync();
            int orderExit = await CliApplication.Build(orderOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "orders", "place", "--menu-id", menuId, $"{drinkId}:1",
            ]).InvokeAsync();
            string orderId = orderOutput.ToString().Trim();
            int getOrderExit = await CliApplication.Build(orderGetOutput, error).Parse(
            [
                "--db", database,
                "--actor", "sommelier",
                "orders", "get", "--id", orderId, "--json",
            ]).InvokeAsync();
            int reservedInventoryExit = await CliApplication.Build(reservedInventoryOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "inventory", "get", "--ingredient-id", ingredientId, "--json",
            ]).InvokeAsync();
            int cancelExit = await CliApplication.Build(TextWriter.Null, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "orders", "cancel", "--id", orderId,
            ]).InvokeAsync();
            int releasedInventoryExit = await CliApplication.Build(releasedInventoryOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "inventory", "get", "--ingredient-id", ingredientId, "--json",
            ]).InvokeAsync();
            int secondOrderExit = await CliApplication.Build(secondOrderOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "orders", "place", "--menu-id", menuId, $"{drinkId}:1",
            ]).InvokeAsync();
            string secondOrderId = secondOrderOutput.ToString().Trim();
            int completeExit = await CliApplication.Build(TextWriter.Null, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "orders", "complete", "--id", secondOrderId,
            ]).InvokeAsync();
            int completedInventoryExit = await CliApplication.Build(completedInventoryOutput, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "inventory", "get", "--ingredient-id", ingredientId, "--json",
            ]).InvokeAsync();
            int addTagExit = await CliApplication.Build(TextWriter.Null, error).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "tags", "add", ingredientId, "source=cli",
            ]).InvokeAsync();
            int listTagExit = await CliApplication.Build(tagOutput, error).Parse(
            [
                "--db", database,
                "--actor", "anonymous",
                "tags", "list", "--json", ingredientId,
            ]).InvokeAsync();
            int listExit = await CliApplication.Build(listOutput, error).Parse(
            [
                "--db", database,
                "--actor", "anonymous",
                "drinks", "list", "--json",
            ]).InvokeAsync();

            Assert.Equal(0, ingredientExit);
            Assert.Equal(0, inventoryExit);
            Assert.Equal(0, drinkExit);
            Assert.Equal(0, menuExit);
            Assert.Equal(0, addDrinkExit);
            Assert.Equal(0, publishExit);
            Assert.Equal(0, showMenuExit);
            Assert.Equal(0, orderExit);
            Assert.Equal(0, getOrderExit);
            Assert.Equal(0, reservedInventoryExit);
            Assert.Equal(0, cancelExit);
            Assert.Equal(0, releasedInventoryExit);
            Assert.Equal(0, secondOrderExit);
            Assert.Equal(0, completeExit);
            Assert.Equal(0, completedInventoryExit);
            Assert.Equal(0, addTagExit);
            Assert.Equal(0, listTagExit);
            Assert.Equal(0, listExit);
            Assert.StartsWith("ing-", ingredientId, StringComparison.Ordinal);
            Assert.Equal(ingredientId, inventoryOutput.ToString().Trim());
            Assert.StartsWith("drk-", drinkOutput.ToString().Trim(), StringComparison.Ordinal);
            Assert.StartsWith("mnu-", menuId, StringComparison.Ordinal);
            Assert.StartsWith("ord-", orderId, StringComparison.Ordinal);
            Assert.Empty(error.ToString());
            using JsonDocument document = JsonDocument.Parse(listOutput.ToString());
            JsonElement drink = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("CLI Gimlet", drink.GetProperty("name").GetString());
            Assert.Equal(
                ingredientId,
                drink.GetProperty("recipe").GetProperty("ingredients")[0]
                    .GetProperty("ingredient_id").GetString());
            using JsonDocument menuDocument = JsonDocument.Parse(menuShowOutput.ToString());
            Assert.Equal("published", menuDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                drinkId,
                menuDocument.RootElement.GetProperty("items")[0].GetProperty("drinkId").GetString());
            using JsonDocument orderDocument = JsonDocument.Parse(orderGetOutput.ToString());
            Assert.Equal(menuId, orderDocument.RootElement.GetProperty("menuId").GetString());
            Assert.Equal(
                ingredientId,
                orderDocument.RootElement.GetProperty("ingredientUsage")[0]
                    .GetProperty("ingredientId").GetString());
            using JsonDocument reservedDocument = JsonDocument.Parse(reservedInventoryOutput.ToString());
            Assert.Equal(12d, reservedDocument.RootElement.GetProperty("quantity").GetDouble());
            Assert.Equal(2d, reservedDocument.RootElement.GetProperty("reserved").GetDouble());
            Assert.Equal(10d, reservedDocument.RootElement.GetProperty("available").GetDouble());
            using JsonDocument releasedDocument = JsonDocument.Parse(releasedInventoryOutput.ToString());
            Assert.Equal(12d, releasedDocument.RootElement.GetProperty("quantity").GetDouble());
            Assert.Equal(0d, releasedDocument.RootElement.GetProperty("reserved").GetDouble());
            using JsonDocument completedDocument = JsonDocument.Parse(completedInventoryOutput.ToString());
            Assert.Equal(10d, completedDocument.RootElement.GetProperty("quantity").GetDouble());
            Assert.Equal(0d, completedDocument.RootElement.GetProperty("reserved").GetDouble());
            using JsonDocument tagDocument = JsonDocument.Parse(tagOutput.ToString());
            Assert.Equal(
                "source=cli",
                tagDocument.RootElement.GetProperty("tags")[0].GetString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AuditCliObservesACommandFromAnEarlierInvocation()
    {
        string root = Path.Combine(Path.GetTempPath(), "mixology-cli-audit", Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "mixology.db");
        StringWriter auditOutput = new();
        StringWriter auditError = new();

        try
        {
            int created = await CliApplication.Build(TextWriter.Null, TextWriter.Null).Parse(
            [
                "--db", database,
                "--actor", "manager",
                "ingredients", "create", "Gin",
                "--category", "spirit",
                "--unit", "oz",
            ]).InvokeAsync();
            int audited = await CliApplication.Build(auditOutput, auditError).Parse(
            [
                "--db", database,
                "--actor", "owner",
                "audit", "list",
                "--json",
            ]).InvokeAsync();

            Assert.Equal(0, created);
            Assert.Equal(0, audited);
            Assert.Empty(auditError.ToString());
            using JsonDocument document = JsonDocument.Parse(auditOutput.ToString());
            JsonElement entry = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("Mixology::Actor::\"manager\"", entry.GetProperty("principal").GetString());
            Assert.Contains("Ingredient::Action", entry.GetProperty("action").GetString(), StringComparison.Ordinal);
            Assert.True(entry.GetProperty("success").GetBoolean());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
