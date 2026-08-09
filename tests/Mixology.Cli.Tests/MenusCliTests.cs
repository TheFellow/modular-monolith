using System.CommandLine;
using System.Text.Json;
using Mixology.Cli;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Money;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Quality;
using Mixology.Kernel.Tags;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Xunit;

namespace Mixology.Cli.Tests;

public sealed class MenusCliTests
{
    [Fact]
    public void CommandTreeMatchesTheGoMenuSurface()
    {
        Harness harness = new();

        Command command = MenusCommands.Build(harness.Context);

        Assert.Equal(
        [
            "readiness", "list", "show", "create", "update", "delete",
            "add-drink", "remove-drink", "publish", "draft",
        ],
            command.Subcommands.Select(static value => value.Name));
    }

    [Fact]
    public async Task ListMapsFiltersPagingAndWritesCanonicalJsonWithoutAnalyzing()
    {
        Harness harness = new();
        Menu menu = Menu("Summer", MenuStatus.Published, [Item()]);
        harness.Session.Menus.Add(menu);
        harness.Session.Page = new Page<Menu>([menu], new Cursor(MenuId.New().Value));
        string cursor = MenuId.New().Value;

        int exitCode = await MenusCommands.Build(harness.Context).Parse(
        [
            "list", "--status", "published", "--filter", "name.contains(\"Summer\")",
            "--cursor", cursor, "--limit", "2", "--costs", "--target-margin", "0.75", "--json",
        ]).InvokeAsync();

        Assert.Equal(0, exitCode);
        ListMenusRequest request = Assert.IsType<ListMenusRequest>(harness.Session.LastList);
        Assert.Equal(MenuStatus.Published, request.Status);
        Assert.Equal("name.contains(\"Summer\")", request.Filter);
        Assert.Equal(cursor, request.Cursor.Value);
        Assert.Equal(2, request.Limit);
        Assert.Null(harness.Session.LastAnalyzeId);
        using JsonDocument document = JsonDocument.Parse(harness.Output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(menu.Id.Value, root.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("published", root.GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal("$12.50", root.GetProperty("items")[0].GetProperty("items")[0].GetProperty("price").GetString());
        Assert.Equal(harness.Session.Page.Next.Value, root.GetProperty("next").GetString());
        Assert.True(harness.Session.Disposed);
    }

    [Fact]
    public async Task FilterHelpAndTemplatesDoNotOpenSessions()
    {
        Harness filter = new();
        int filterExit = await MenusCommands.Build(filter.Context).Parse(
            ["list", "--filter-help"]).InvokeAsync();

        Assert.Equal(0, filterExit);
        Assert.Equal(0, filter.SessionCreations);
        Assert.Contains("created_at", filter.Output.ToString(), StringComparison.Ordinal);

        Harness create = new();
        int createExit = await MenusCommands.Build(create.Context).Parse(
            ["create", "--template"]).InvokeAsync();
        Harness update = new();
        int updateExit = await MenusCommands.Build(update.Context).Parse(
            ["update", "--template"]).InvokeAsync();

        Assert.Equal(0, createExit);
        Assert.Equal(0, updateExit);
        Assert.Equal(0, create.SessionCreations);
        Assert.Equal(0, update.SessionCreations);
        Assert.Contains("Summer Cocktails", create.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("mnu-...", update.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadinessRendersHumanFindingsAndCanonicalJson()
    {
        Menu menu = Menu("Blocked", MenuStatus.Draft, [Item()]);
        ReadinessFinding finding = new(
            ReadinessSeverity.Blocker,
            ReadinessCode.Unavailable,
            menu.Items[0].DrinkId,
            null,
            "drink is unavailable");
        Harness human = new();
        human.Session.Report = new ReadinessReport(menu.Id, menu.Status, [finding]);

        int humanExit = await MenusCommands.Build(human.Context).Parse(
            ["readiness", "--id", menu.Id.Value]).InvokeAsync();

        Assert.Equal(0, humanExit);
        Assert.Contains("blocker\tunavailable\tdrink is unavailable", human.Output.ToString(), StringComparison.Ordinal);

        Harness json = new();
        json.Session.Report = human.Session.Report;
        int jsonExit = await MenusCommands.Build(json.Context).Parse(
            ["readiness", "--id", menu.Id.Value, "--json"]).InvokeAsync();

        Assert.Equal(0, jsonExit);
        using JsonDocument document = JsonDocument.Parse(json.Output.ToString());
        Assert.Equal(menu.Id.Value, document.RootElement.GetProperty("menuId").GetString());
        Assert.Equal("unavailable", document.RootElement.GetProperty("findings")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ShowRendersItemsOrTargetMarginAnalysis()
    {
        Menu menu = Menu("Dinner", MenuStatus.Draft, [Item()]);
        Harness plain = new();
        plain.Session.Menus.Add(menu);

        int plainExit = await MenusCommands.Build(plain.Context).Parse(
            ["show", "--id", menu.Id.Value]).InvokeAsync();

        Assert.Equal(0, plainExit);
        Assert.Contains("DRINK_ID\tDISPLAY_NAME\tPRICE", plain.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("$12.50", plain.Output.ToString(), StringComparison.Ordinal);

        Harness costs = new();
        costs.Session.Menus.Add(menu);
        costs.Session.Analysis = Analysis(menu);
        int costsExit = await MenusCommands.Build(costs.Context).Parse(
            ["show", "--id", menu.Id.Value, "--costs", "--target-margin", "0.8"]).InvokeAsync();

        Assert.Equal(0, costsExit);
        Assert.Equal(0.8, costs.Session.LastTargetMargin);
        Assert.Contains("DRINK_ID\tNAME\tCOST\tPRICE\tMARGIN\tSTATUS", costs.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Average margin:\t60%", costs.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("sub:", costs.Output.ToString(), StringComparison.Ordinal);

        Harness json = new();
        json.Session.Menus.Add(menu);
        json.Session.Analysis = Analysis(menu);
        int jsonExit = await MenusCommands.Build(json.Context).Parse(
            ["show", "--id", menu.Id.Value, "--costs", "--json"]).InvokeAsync();

        Assert.Equal(0, jsonExit);
        using JsonDocument document = JsonDocument.Parse(json.Output.ToString());
        Assert.Equal(0.6, document.RootElement.GetProperty("averageMargin").GetDouble());
        Assert.Equal(
            "equivalent",
            document.RootElement.GetProperty("items")[0]
                .GetProperty("substitutions")[0]
                .GetProperty("qualityImpact")
                .GetString());
    }

    [Fact]
    public async Task CreateSupportsArgumentsAndStructuredStdin()
    {
        Harness direct = new();
        int directExit = await MenusCommands.Build(direct.Context).Parse(["create", "Dinner"]).InvokeAsync();

        Assert.Equal(0, directExit);
        Assert.Equal("Dinner", direct.Session.LastCreate?.Name);
        Assert.Equal(direct.Session.Menus[^1].Id.Value, direct.Output.ToString().Trim());

        Harness stdin = new("""{"name":"Late Dinner","description":"After hours"}""");
        int stdinExit = await MenusCommands.Build(stdin.Context).Parse(
            ["create", "--stdin", "--json"]).InvokeAsync();

        Assert.Equal(0, stdinExit);
        Assert.Equal("Late Dinner", stdin.Session.LastCreate?.Name);
        Assert.Equal("After hours", stdin.Session.LastCreate?.Description);
        using JsonDocument document = JsonDocument.Parse(stdin.Output.ToString());
        Assert.Equal("Late Dinner", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateFlagsLoadCurrentStateAndStructuredInputMapsDirectly()
    {
        Menu existing = Menu("Dinner", MenuStatus.Draft, []);
        Harness flags = new();
        flags.Session.Menus.Add(existing);
        int flagsExit = await MenusCommands.Build(flags.Context).Parse(
            ["update", "--id", existing.Id.Value, "--name", "Late Dinner"]).InvokeAsync();

        Assert.Equal(0, flagsExit);
        Assert.Equal(existing.Id, flags.Session.LastUpdate?.Id);
        Assert.Equal("Late Dinner", flags.Session.LastUpdate?.Name);
        Assert.Equal(existing.Description, flags.Session.LastUpdate?.Description);

        MenuId id = MenuId.New();
        Harness stdin = new($$"""{"id":"{{id.Value}}","name":"Brunch","description":"Sunday"}""");
        int stdinExit = await MenusCommands.Build(stdin.Context).Parse(
            ["update", "--stdin"]).InvokeAsync();

        Assert.Equal(0, stdinExit);
        Assert.Equal(id, stdin.Session.LastUpdate?.Id);
        Assert.Equal("Brunch", stdin.Session.LastUpdate?.Name);
        Assert.Equal("Sunday", stdin.Session.LastUpdate?.Description);
    }

    [Fact]
    public async Task LifecycleAndDrinkMutationsMapIdsAndRenderCanonicalJsonOrIds()
    {
        Menu menu = Menu("Service", MenuStatus.Draft, []);
        DrinkId drink = DrinkId.New();
        Harness add = WithMenu(menu);
        int addExit = await MenusCommands.Build(add.Context).Parse(
            ["add-drink", "--menu-id", menu.Id.Value, "--drink-id", drink.Value]).InvokeAsync();
        Assert.Equal(0, addExit);
        Assert.Equal(menu.Id, add.Session.LastAdd?.MenuId);
        Assert.Equal(drink, add.Session.LastAdd?.DrinkId);

        Harness remove = WithMenu(menu with { Items = [Item(drink)] });
        int removeExit = await MenusCommands.Build(remove.Context).Parse(
            ["remove-drink", "--menu-id", menu.Id.Value, "--drink-id", drink.Value]).InvokeAsync();
        Assert.Equal(0, removeExit);
        Assert.Equal(drink, remove.Session.LastRemove?.DrinkId);

        foreach (string verb in new[] { "delete", "publish", "draft" })
        {
            Harness harness = WithMenu(menu);
            int exitCode = await MenusCommands.Build(harness.Context).Parse(
                [verb, "--id", menu.Id.Value, "--json"]).InvokeAsync();
            Assert.Equal(0, exitCode);
            using JsonDocument document = JsonDocument.Parse(harness.Output.ToString());
            Assert.Equal(menu.Id.Value, document.RootElement.GetProperty("id").GetString());
        }
    }

    [Fact]
    public async Task InvalidMarginAndSessionErrorsUseTypedExitsAndDispose()
    {
        Menu menu = Menu("Dinner", MenuStatus.Draft, [Item()]);
        Harness invalid = WithMenu(menu);
        int invalidExit = await MenusCommands.Build(invalid.Context).Parse(
            ["show", "--id", menu.Id.Value, "--costs", "--target-margin", "1.2"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitInvalid, invalidExit);
        Assert.Contains("target margin", invalid.Error.ToString(), StringComparison.Ordinal);
        Assert.True(invalid.Session.Disposed);

        Harness denied = new();
        denied.Session.Exception = AppError.Permission("menu denied");
        int deniedExit = await MenusCommands.Build(denied.Context).Parse(["list"]).InvokeAsync();

        Assert.Equal(ErrorCatalog.ExitPermission, deniedExit);
        Assert.Equal("menu denied", denied.Error.ToString().Trim());
        Assert.True(denied.Session.Disposed);
    }

    private static Harness WithMenu(Menu menu)
    {
        Harness harness = new();
        harness.Session.Menus.Add(menu);
        return harness;
    }

    private sealed class Harness
    {
        public Harness(string input = "")
        {
            Context = new MenusCommandContext(
                (_, _) =>
                {
                    SessionCreations++;
                    return ValueTask.FromResult<IMenusCommandSession>(Session);
                },
                Output,
                Error,
                new StringReader(input));
        }

        public FakeSession Session { get; } = new();
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public MenusCommandContext Context { get; }
        public int SessionCreations { get; private set; }
    }

    private sealed class FakeSession : IMenusCommandSession
    {
        public List<Menu> Menus { get; } = [];
        public Page<Menu>? Page { get; set; }
        public ReadinessReport? Report { get; set; }
        public MenuAnalysis? Analysis { get; set; }
        public ListMenusRequest? LastList { get; private set; }
        public MenuId? LastAnalyzeId { get; private set; }
        public double? LastTargetMargin { get; private set; }
        public CreateMenuRequest? LastCreate { get; private set; }
        public UpdateMenuRequest? LastUpdate { get; private set; }
        public AddMenuItemRequest? LastAdd { get; private set; }
        public RemoveMenuItemRequest? LastRemove { get; private set; }
        public Exception? Exception { get; set; }
        public bool Disposed { get; private set; }

        public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastList = request;
            return Exception is null
                ? Task.FromResult(Page ?? new Page<Menu>(Menus, default))
                : Task.FromException<Page<Menu>>(Exception);
        }

        public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Menus.Single(menu => menu.Id == id));
        }

        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Report ?? new ReadinessReport(id, MenuStatus.Draft, []));
        }

        public Task<MenuAnalysis> AnalyzeAsync(
            MenuId id,
            double targetMargin,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastAnalyzeId = id;
            LastTargetMargin = targetMargin;
            Menu menu = Menus.Single(value => value.Id == id);
            return Task.FromResult(Analysis ?? new MenuAnalysis(menu, [], 0, menu.Items.Count, null));
        }

        public Task<Menu> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCreate = request;
            Menu menu = Menu(request.Name, MenuStatus.Draft, [], request.Description);
            Menus.Add(menu);
            return Task.FromResult(menu);
        }

        public Task<Menu> UpdateAsync(UpdateMenuRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUpdate = request;
            Menu? current = Menus.SingleOrDefault(menu => menu.Id == request.Id);
            Menu updated = current is null
                ? Menu(request.Name, MenuStatus.Draft, [], request.Description) with { Id = request.Id }
                : current with { Name = request.Name, Description = request.Description };
            return Task.FromResult(updated);
        }

        public Task<Menu> DeleteAsync(MenuId id, CancellationToken cancellationToken) =>
            GetAsync(id, cancellationToken);

        public Task<Menu> AddDrinkAsync(AddMenuItemRequest request, CancellationToken cancellationToken)
        {
            LastAdd = request;
            return GetAsync(request.MenuId, cancellationToken);
        }

        public Task<Menu> RemoveDrinkAsync(RemoveMenuItemRequest request, CancellationToken cancellationToken)
        {
            LastRemove = request;
            return GetAsync(request.MenuId, cancellationToken);
        }

        public Task<Menu> PublishAsync(MenuId id, CancellationToken cancellationToken) =>
            GetAsync(id, cancellationToken);

        public Task<Menu> DraftAsync(MenuId id, CancellationToken cancellationToken) =>
            GetAsync(id, cancellationToken);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private static Menu Menu(
        string name,
        MenuStatus status,
        IReadOnlyList<MenuItem> items,
        string description = "Evening service") => new(
        MenuId.New(),
        name,
        description,
        items,
        status,
        new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero),
        status == MenuStatus.Published ? new DateTimeOffset(2026, 8, 10, 2, 0, 0, TimeSpan.Zero) : null,
        null,
        TagCollection.Empty);

    private static MenuItem Item(DrinkId? id = null) => new(
        id ?? DrinkId.New(),
        "House pour",
        new Price(12.5m, Currency.Usd),
        true,
        Availability.Limited,
        0);

    private static MenuAnalysis Analysis(Menu menu)
    {
        AppliedSubstitution substitution = new(
            IngredientId.New(),
            IngredientId.New(),
            1,
            Quality.Equivalent);
        MenuItem item = menu.Items[0];
        return new MenuAnalysis(
            menu,
            [new MenuItemAnalysis(
                item.DrinkId,
                "House pour",
                item.Availability,
                [substitution],
                new Price(5m, Currency.Usd),
                false,
                item.Price,
                0.6,
                new Price(12.5m, Currency.Usd))],
            1,
            1,
            0.6);
    }
}
