using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class IngredientsCliTests
{
    [Fact]
    public void CommandTreeExposesTheCompleteInitialSurfaceWithoutDeferredFlags()
    {
        Harness harness = new();
        Command command = IngredientsCommands.Build(harness.Context);

        Assert.Equal(["list", "get", "create", "update", "retire"], command.Subcommands.Select(value => value.Name));
        Assert.Contains("delete", command.Subcommands.Single(value => value.Name == "retire").Aliases);
        ParseResult unsupported = command.Parse(
            ["create", "Gin", "--category", "spirit", "--unit", "oz", "--stdin"]);
        Assert.NotEmpty(unsupported.Errors);
    }

    [Fact]
    public async Task ListMapsEveryRequestFlagAndWritesCanonicalJson()
    {
        Harness harness = new();
        Ingredient first = harness.Add("Botanical Gin", IngredientCategory.Spirit, Unit.Ounce);
        Ingredient second = harness.Add("Tonic", IngredientCategory.Mixer, Unit.Milliliter);
        string cursor = IngredientId.New().Value;

        int exitCode = await IngredientsCommands.Build(harness.Context).Parse(
            ["list", "--category", "spirit", "--filter", "name.contains(\"Gin\")", "--limit", "2", "--cursor", cursor, "--json"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(harness.Error.ToString());
        Assert.Equal(IngredientCategory.Spirit, harness.Session.LastList?.Category);
        Assert.Equal("name.contains(\"Gin\")", harness.Session.LastList?.Filter);
        Assert.Equal(2, harness.Session.LastList?.Limit);
        Assert.Equal(cursor, harness.Session.LastList?.Cursor.Value);
        using JsonDocument json = JsonDocument.Parse(harness.Output.ToString());
        JsonElement root = json.RootElement;
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.Equal(first.Id.Value, root.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("Botanical Gin", root.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal(second.Id.Value, root.GetProperty("items")[1].GetProperty("id").GetString());
        Assert.False(root.GetProperty("items")[0].TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task FilterHelpDoesNotOpenAnApplicationSession()
    {
        Harness harness = new();

        int exitCode = await IngredientsCommands.Build(harness.Context)
            .Parse(["list", "--filter-help"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(0, harness.SessionCreations);
        Assert.Contains("category", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("tags contains", harness.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetWritesCanonicalLowercaseJson()
    {
        Harness harness = new();
        Ingredient ingredient = harness.Add("Gin", IngredientCategory.Spirit, Unit.Ounce);

        int exitCode = await IngredientsCommands.Build(harness.Context)
            .Parse(["get", "--id", ingredient.Id.Value, "--json"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        using JsonDocument json = JsonDocument.Parse(harness.Output.ToString());
        Assert.Equal(ingredient.Id.Value, json.RootElement.GetProperty("id").GetString());
        Assert.Equal("spirit", json.RootElement.GetProperty("category").GetString());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("tags").ValueKind);
        Assert.False(json.RootElement.TryGetProperty("Id", out _));
    }

    [Fact]
    public async Task CreateMapsOptionsAndWritesOnlyTheId()
    {
        Harness harness = new();

        int exitCode = await IngredientsCommands.Build(harness.Context).Parse(
            ["create", "House Gin", "--category", "spirit", "--unit", "oz", "--description", "Dry"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("House Gin", harness.Session.LastCreate?.Name);
        Assert.Equal(IngredientCategory.Spirit, harness.Session.LastCreate?.Category);
        Assert.Equal(Unit.Ounce, harness.Session.LastCreate?.Unit);
        Assert.Equal("Dry", harness.Session.LastCreate?.Description);
        Assert.Equal(harness.Session.Items[^1].Id.Value, harness.Output.ToString().Trim());
        Assert.DoesNotContain('{', harness.Output.ToString());
    }

    [Fact]
    public async Task UpdateMapsPatchOptionsAndWritesOnlyTheId()
    {
        Harness harness = new();
        Ingredient ingredient = harness.Add("Gin", IngredientCategory.Spirit, Unit.Ounce);

        int exitCode = await IngredientsCommands.Build(harness.Context).Parse(
            ["update", "--id", ingredient.Id.Value, "--name", "Dry Gin", "--category", "other", "--unit", "ml", "--description", "Updated"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(ingredient.Id, harness.Session.LastUpdate?.Id);
        Assert.Equal("Dry Gin", harness.Session.LastUpdate?.Name);
        Assert.Equal(IngredientCategory.Other, harness.Session.LastUpdate?.Category);
        Assert.Equal(Unit.Milliliter, harness.Session.LastUpdate?.Unit);
        Assert.Equal("Updated", harness.Session.LastUpdate?.Description);
        Assert.Equal(ingredient.Id.Value, harness.Output.ToString().Trim());
    }

    [Theory]
    [InlineData("retire")]
    [InlineData("delete")]
    public async Task RetireAndDeleteShareReplacementSemantics(string command)
    {
        Harness harness = new();
        Ingredient source = harness.Add("Gin", IngredientCategory.Spirit, Unit.Ounce);
        Ingredient replacement = harness.Add("Vodka", IngredientCategory.Spirit, Unit.Ounce);

        int exitCode = await IngredientsCommands.Build(harness.Context).Parse(
            [command, "--id", source.Id.Value, "--replacement-id", replacement.Id.Value, "--replacement-ratio", "0.75"])
            .InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(source.Id, harness.Session.LastRetire?.Id);
        Assert.Equal(replacement.Id, harness.Session.LastRetire?.Retirement.ReplacementId);
        Assert.Equal(0.75, harness.Session.LastRetire?.Retirement.Ratio);
        Assert.Equal(source.Id.Value, harness.Output.ToString().Trim());
    }

    [Fact]
    public async Task InvalidCategoryUsesTheSharedTypedErrorContract()
    {
        Harness harness = new();

        int exitCode = await IngredientsCommands.Build(harness.Context).Parse(
            ["create", "Gin", "--category", "invalid", "--unit", "oz"])
            .InvokeAsync();

        Assert.Equal(10, exitCode);
        Assert.Empty(harness.Output.ToString());
        Assert.Equal("invalid category \"invalid\"", harness.Error.ToString().Trim());
    }

    private sealed class Harness
    {
        public Harness()
        {
            Context = new IngredientsCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<IIngredientsCommandSession>(Session);
                },
                Output,
                Error);
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public IngredientsCommandContext Context { get; }
        public int SessionCreations { get; private set; }

        public Ingredient Add(string name, IngredientCategory category, Unit unit)
        {
            Ingredient ingredient = NewIngredient(name, category, unit);
            Session.Items.Add(ingredient);
            return ingredient;
        }
    }

    private sealed class FakeSession : IIngredientsCommandSession
    {
        public List<Ingredient> Items { get; } = [];
        public ListIngredientsRequest? LastList { get; private set; }
        public CreateIngredientRequest? LastCreate { get; private set; }
        public UpdateIngredientRequest? LastUpdate { get; private set; }
        public RetireIngredientRequest? LastRetire { get; private set; }

        public Task<Page<Ingredient>> ListAsync(
            ListIngredientsRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastList = request;
            return Task.FromResult(new Page<Ingredient>(Items, default));
        }

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Items.Single(value => value.Id == id));
        }

        public Task<Ingredient> CreateAsync(
            CreateIngredientRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCreate = request;
            Ingredient ingredient = NewIngredient(request.Name, request.Category, request.Unit, request.Description);
            Items.Add(ingredient);
            return Task.FromResult(ingredient);
        }

        public Task<Ingredient> UpdateAsync(
            UpdateIngredientRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUpdate = request;
            Ingredient current = Items.Single(value => value.Id == request.Id);
            Ingredient updated = current with
            {
                Name = request.Name ?? current.Name,
                Category = request.Category ?? current.Category,
                Unit = request.Unit ?? current.Unit,
                Description = request.Description ?? current.Description,
            };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult(updated);
        }

        public Task<Ingredient> RetireAsync(
            RetireIngredientRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRetire = request;
            Ingredient current = Items.Single(value => value.Id == request.Id);
            Ingredient retired = current with { DeletedAt = DateTimeOffset.UtcNow };
            Items[Items.IndexOf(current)] = retired;
            return Task.FromResult(retired);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static Ingredient NewIngredient(
        string name,
        IngredientCategory category,
        Unit unit,
        string description = "") => new(
            IngredientId.New(),
            name,
            category,
            unit,
            description,
            null,
            TagCollection.Empty);
}
