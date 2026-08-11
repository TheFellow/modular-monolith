using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Measurement;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class DrinksCliTests
{
    [Fact]
    public void CommandTreeExposesTheGoSurfaceWithoutUnsupportedFlags()
    {
        Harness harness = new();
        Command command = DrinksCommands.Build(harness.Context);

        Assert.Equal(["list", "get", "create", "update", "delete"], command.Subcommands.Select(value => value.Name));
        Assert.NotEmpty(command.Parse(["create", "--recipe", "recipe.json"]).Errors);
        Assert.NotEmpty(command.Parse(["update", "--tags", "featured"]).Errors);
    }

    [Fact]
    public async Task FilterHelpDoesNotCreateASession()
    {
        Harness harness = new();

        int exitCode = await DrinksCommands.Build(harness.Context).Parse(["list", "--filter-help"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(0, harness.SessionCreations);
        Assert.Contains("recipe.garnish", harness.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("review_required", harness.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListMapsEveryRequestFlagAndWritesCanonicalJson()
    {
        Harness harness = new();
        IngredientId primary = IngredientId.New();
        IngredientId substitute = IngredientId.New();
        Drink drink = harness.Add(
            "House Sour",
            DrinkCategory.Sour,
            GlassType.Coupe,
            new Recipe(
                [new RecipeIngredient(primary, Amount.Create(1.5, Unit.Ounce), true, [substitute])],
                ["Shake", "Strain"],
                "lemon twist"),
            TagCollection.Parse("region=west,featured"));
        Cursor next = DrinkId.New().Value;
        harness.Session.Next = next;
        string cursor = DrinkId.New().Value;

        int exitCode = await DrinksCommands.Build(harness.Context).Parse(
        [
            "list",
            "--name", "House Sour",
            "--category", "sour",
            "--glass", "coupe",
            "--filter", "recipe.garnish.startsWith(\"lemon\")",
            "--limit", "2",
            "--cursor", cursor,
            "--json",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(harness.Error.ToString());
        Assert.Equal("House Sour", harness.Session.LastList?.Name);
        Assert.Equal(DrinkCategory.Sour, harness.Session.LastList?.Category);
        Assert.Equal(GlassType.Coupe, harness.Session.LastList?.Glass);
        Assert.Equal("recipe.garnish.startsWith(\"lemon\")", harness.Session.LastList?.Filter);
        Assert.Equal(2, harness.Session.LastList?.Limit);
        Assert.Equal(cursor, harness.Session.LastList?.Cursor.Value);

        using JsonDocument json = JsonDocument.Parse(harness.Output.ToString());
        Assert.Equal(next.Value, json.RootElement.GetProperty("next").GetString());
        JsonElement item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(drink.Id.Value, item.GetProperty("id").GetString());
        Assert.Equal("sour", item.GetProperty("category").GetString());
        Assert.Equal("active", item.GetProperty("status").GetString());
        Assert.Equal(["featured", "region=west"], item.GetProperty("tags").EnumerateArray().Select(value => value.GetString()));
        JsonElement ingredient = Assert.Single(
            item.GetProperty("recipe").GetProperty("ingredients").EnumerateArray());
        Assert.Equal(primary.Value, ingredient.GetProperty("ingredient_id").GetString());
        Assert.Equal(1.5, ingredient.GetProperty("amount").GetDouble());
        Assert.Equal("oz", ingredient.GetProperty("unit").GetString());
        Assert.True(ingredient.GetProperty("optional").GetBoolean());
        Assert.Equal(substitute.Value, Assert.Single(ingredient.GetProperty("substitutes").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task HumanListAndGetUseTheReferenceSummaryShape()
    {
        Harness list = new();
        Drink drink = list.Add("Highball", DrinkCategory.Highball, GlassType.Highball, RecipeWithOneIngredient());

        int listed = await DrinksCommands.Build(list.Context).Parse(["list"]).InvokeAsync();

        Assert.Equal(0, listed);
        Assert.StartsWith("ID\tNAME\tCATEGORY\tGLASS\tSTATUS\tINGREDIENTS\tTAGS", list.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains($"{drink.Id.Value}\tHighball\thighball\thighball\tactive\t1", list.Output.ToString(), StringComparison.Ordinal);

        Harness get = new();
        get.Session.Items.Add(drink);
        int shown = await DrinksCommands.Build(get.Context).Parse(["get", "--id", drink.Id.Value]).InvokeAsync();

        Assert.Equal(0, shown);
        Assert.Contains($"ID:\t{drink.Id.Value}", get.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Ingredients:\t1", get.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateReadsStructuredJsonFromStdinAndPrintsOnlyTheId()
    {
        IngredientId ingredient = IngredientId.New();
        IngredientId substitute = IngredientId.New();
        string input = $$"""
            {
              "name": "Margarita",
              "category": "cocktail",
              "glass": "coupe",
              "description": "A classic sour",
              "recipe": {
                "ingredients": [{
                  "ingredient_id": "{{ingredient.Value}}",
                  "amount": 2,
                  "unit": "oz",
                  "optional": true,
                  "substitutes": ["{{substitute.Value}}"]
                }],
                "steps": ["Shake", "Strain"],
                "garnish": "lime wheel"
              }
            }
            """;
        Harness harness = new(input);

        int exitCode = await DrinksCommands.Build(harness.Context).Parse(["create", "--stdin"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(harness.Error.ToString());
        CreateDrinkRequest request = Assert.IsType<CreateDrinkRequest>(harness.Session.LastCreate);
        Assert.Equal("Margarita", request.Name);
        Assert.Equal(DrinkCategory.Cocktail, request.Category);
        Assert.Equal(GlassType.Coupe, request.Glass);
        RecipeIngredient mapped = Assert.Single(request.Recipe.Ingredients);
        Assert.Equal(ingredient, mapped.IngredientId);
        Assert.Equal(2, mapped.Amount.Value);
        Assert.Equal(Unit.Ounce, mapped.Amount.Unit);
        Assert.True(mapped.Optional);
        Assert.Equal(substitute, Assert.Single(mapped.Substitutes));
        Assert.Equal(harness.Session.Items.Single().Id.Value, harness.Output.ToString().Trim());
    }

    [Fact]
    public async Task UpdateReadsAFileAndWritesTheFullCanonicalJsonView()
    {
        DrinkId id = DrinkId.New();
        IngredientId ingredient = IngredientId.New();
        string directory = Path.Combine(Path.GetTempPath(), "mixology-drinks-cli", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "update.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, $$"""
            {
              "id": "{{id.Value}}",
              "revision": 1,
              "name": "Updated Sour",
              "category": "sour",
              "glass": "rocks",
              "recipe": {
                "ingredients": [{"ingredient_id": "{{ingredient.Value}}", "amount": 1, "unit": "oz"}],
                "steps": ["Stir"]
              }
            }
            """);
        Harness harness = new();

        try
        {
            int exitCode = await DrinksCommands.Build(harness.Context).Parse(
                ["update", "--file", path, "--json"]).InvokeAsync();

            Assert.Equal(0, exitCode);
            Assert.Equal(id, harness.Session.LastUpdate?.Id);
            Assert.Equal("Updated Sour", harness.Session.LastUpdate?.Name);
            using JsonDocument json = JsonDocument.Parse(harness.Output.ToString());
            Assert.Equal(id.Value, json.RootElement.GetProperty("id").GetString());
            Assert.Equal("active", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(ingredient.Value, json.RootElement.GetProperty("recipe")
                .GetProperty("ingredients")[0]
                .GetProperty("ingredient_id")
                .GetString());
            Assert.False(json.RootElement.GetProperty("recipe").GetProperty("ingredients")[0]
                .TryGetProperty("substitutes", out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("both")]
    [InlineData("empty")]
    [InlineData("malformed")]
    public async Task StructuredInputFailuresUseTypedInvalidErrors(string scenario)
    {
        string input = scenario switch
        {
            "empty" => " ",
            "malformed" => "{",
            _ => "{}",
        };
        Harness harness = new(input);
        string[] arguments = scenario switch
        {
            "both" => ["create", "--stdin", "--file", "drink.json"],
            "missing" => ["create"],
            _ => ["create", "--stdin"],
        };

        int exitCode = await DrinksCommands.Build(harness.Context).Parse(arguments).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, exitCode);
        Assert.Empty(harness.Output.ToString());
        Assert.NotEmpty(harness.Error.ToString());
    }

    [Fact]
    public async Task TemplateDoesNotCreateASessionAndUsesSnakeCaseIngredientIds()
    {
        Harness harness = new();

        int exitCode = await DrinksCommands.Build(harness.Context).Parse(["create", "--template"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(0, harness.SessionCreations);
        using JsonDocument json = JsonDocument.Parse(harness.Output.ToString());
        JsonElement ingredient = json.RootElement.GetProperty("recipe").GetProperty("ingredients")[0];
        Assert.True(ingredient.TryGetProperty("ingredient_id", out _));
        Assert.False(ingredient.TryGetProperty("ingredientId", out _));
    }

    [Fact]
    public async Task DeletePrintsTheIdAndPreservesTypedPermissionFailures()
    {
        Harness success = new();
        Drink drink = success.Add("Martini", DrinkCategory.Martini, GlassType.Martini, RecipeWithOneIngredient());

        int deleted = await DrinksCommands.Build(success.Context).Parse(["delete", "--id", drink.Id.Value])
            .InvokeAsync();

        Assert.Equal(0, deleted);
        Assert.Equal(drink.Id, success.Session.LastDelete);
        Assert.Equal(drink.Id.Value, success.Output.ToString().Trim());

        Harness denied = new();
        denied.Session.Failure = AppError.Permission("drink delete denied");
        int deniedCode = await DrinksCommands.Build(denied.Context).Parse(["delete", "--id", drink.Id.Value])
            .InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitPermission, deniedCode);
        Assert.Equal("drink delete denied", denied.Error.ToString().Trim());
    }

    private sealed class Harness
    {
        public Harness(string input = "")
        {
            Context = new DrinksCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<IDrinksCommandSession>(Session);
                },
                new StringReader(input),
                Output,
                Error);
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public DrinksCommandContext Context { get; }
        public int SessionCreations { get; private set; }

        public Drink Add(
            string name,
            DrinkCategory category,
            GlassType glass,
            Recipe recipe,
            TagCollection? tags = null)
        {
            Drink drink = NewDrink(DrinkId.New(), name, category, glass, recipe, tags ?? TagCollection.Empty);
            Session.Items.Add(drink);
            return drink;
        }
    }

    private sealed class FakeSession : IDrinksCommandSession
    {
        public List<Drink> Items { get; } = [];
        public Cursor Next { get; set; }
        public Exception? Failure { get; set; }
        public ListDrinksRequest? LastList { get; private set; }
        public CreateDrinkRequest? LastCreate { get; private set; }
        public UpdateDrinkRequest? LastUpdate { get; private set; }
        public DrinkId? LastDelete { get; private set; }

        public Task<Page<Drink>> ListAsync(ListDrinksRequest request, CancellationToken cancellationToken)
        {
            RequireSuccess(cancellationToken);
            LastList = request;
            return Task.FromResult(new Page<Drink>(Items, Next));
        }

        public Task<Drink> GetAsync(DrinkId id, CancellationToken cancellationToken)
        {
            RequireSuccess(cancellationToken);
            return Task.FromResult(Items.Single(value => value.Id == id));
        }

        public Task<Drink> CreateAsync(CreateDrinkRequest request, CancellationToken cancellationToken)
        {
            RequireSuccess(cancellationToken);
            LastCreate = request;
            CreateDrinkRequest normalized = request.Normalize();
            Drink drink = NewDrink(
                DrinkId.New(),
                normalized.Name,
                normalized.Category,
                normalized.Glass,
                normalized.Recipe,
                TagCollection.Empty,
                normalized.Description);
            Items.Add(drink);
            return Task.FromResult(drink);
        }

        public Task<Drink> UpdateAsync(UpdateDrinkRequest request, CancellationToken cancellationToken)
        {
            RequireSuccess(cancellationToken);
            LastUpdate = request;
            UpdateDrinkRequest normalized = request.Normalize();
            Drink drink = NewDrink(
                normalized.Id,
                normalized.Name,
                normalized.Category,
                normalized.Glass,
                normalized.Recipe,
                TagCollection.Empty,
                normalized.Description);
            Items.Add(drink);
            return Task.FromResult(drink);
        }

        public Task<Drink> DeleteAsync(DrinkId id, CancellationToken cancellationToken)
        {
            RequireSuccess(cancellationToken);
            LastDelete = id;
            Drink current = Items.Single(value => value.Id == id);
            return Task.FromResult(current with { DeletedAt = DateTimeOffset.UtcNow });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void RequireSuccess(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    private static Recipe RecipeWithOneIngredient() => new(
        [new RecipeIngredient(IngredientId.New(), Amount.Create(1, Unit.Ounce))],
        ["Mix"]);

    private static Drink NewDrink(
        DrinkId id,
        string name,
        DrinkCategory category,
        GlassType glass,
        Recipe recipe,
        TagCollection tags,
        string description = "") => new(
            id,
            name,
            category,
            glass,
            recipe,
            description,
            DrinkStatus.Active,
            null,
            tags);
}
