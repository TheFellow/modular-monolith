using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Dispatcher;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Mixology.Kernel.Tags;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Models;
using Mixology.Modules.Audit.Requests;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Models;
using Mixology.Modules.Drinks.Requests;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Models;
using Mixology.Modules.Ingredients.Requests;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Models;
using Mixology.Modules.Inventory.Requests;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Models;
using Mixology.Modules.Menus.Requests;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Models;
using Mixology.Modules.Orders.Requests;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence;
using Mixology.Presentation;
using Mixology.Presentation.Dashboard;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using DashboardData = Mixology.Presentation.Dashboard.Dashboard;

namespace Mixology.Cli;

public static class CliApplication
{
    public static RootCommand Build(
        TextWriter? output = null,
        TextWriter? error = null,
        TextReader? input = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        input ??= Console.In;
        RootCommand root = new("Manage the Mixology modular-monolith application.");
        Option<string> database = new("--db")
        {
            Description = "Path to the device-local Mixology SQLite database.",
            DefaultValueFactory = _ => Path.GetFullPath("mixology.db"),
        };
        Option<string> actor = new("--actor", "--as")
        {
            Description = "Actor identity: owner, manager, sommelier, bartender, or anonymous.",
            DefaultValueFactory = _ => "owner",
        };
        Option<string> logLevel = new("--log-level")
        {
            Description = "Diagnostic level: debug, info, warn, or error.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_LEVEL") ?? "info",
        };
        Option<string> logFormat = new("--log-format")
        {
            Description = "Diagnostic format: text or json.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FORMAT") ?? "text",
        };
        Option<string> logFile = new("--log-file")
        {
            Description = "Write diagnostics to this file instead of stderr.",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("MIXOLOGY_LOG_FILE") ?? string.Empty,
        };
        Option<bool> metrics = new("--metrics")
        {
            Description = "Expose Prometheus metrics on localhost:9090/metrics for this invocation.",
            DefaultValueFactory = _ => EnvironmentBoolean("MIXOLOGY_METRICS"),
        };
        root.Options.Add(database);
        root.Options.Add(actor);
        root.Options.Add(logLevel);
        root.Options.Add(logFormat);
        root.Options.Add(logFile);
        root.Options.Add(metrics);

        IngredientsCommandContext ingredientsContext = new(
            async (parseResult, cancellationToken) => await HostedIngredientsCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);
        AuditCommandContext auditContext = new(
            async (parseResult, cancellationToken) => await HostedAuditCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);
        DrinksCommandContext drinksContext = new(
            async (parseResult, cancellationToken) => await HostedDrinksCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            input,
            output,
            error);
        InventoryCommandContext inventoryContext = new(
            async (parseResult, cancellationToken) => await HostedInventoryCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);
        MenusCommandContext menusContext = new(
            async (parseResult, cancellationToken) => await HostedMenusCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            output,
            error,
            input);
        OrdersCommandContext ordersContext = new(
            async (parseResult, cancellationToken) => await HostedOrdersCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            output,
            error,
            input);
        TagsCommandContext tagsContext = new(
            async (parseResult, cancellationToken) => await HostedTagsCommandSession.OpenAsync(
                parseResult.GetValue(database) ?? throw AppError.Invalid("database path is required"),
                Actor.Parse(parseResult.GetValue(actor)),
                CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics)),
                cancellationToken).ConfigureAwait(false),
            output,
            error);

        Option<bool> statusJson = new("--json") { Description = "Write the dashboard as JSON." };
        Command status = new("status", "Show the application dashboard aggregate.");
        status.Options.Add(statusJson);
        status.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                string databasePath = parseResult.GetValue(database)
                    ?? throw AppError.Invalid("database path is required");
                Actor principal = Actor.Parse(parseResult.GetValue(actor));
                CliHostOptions hostOptions = CliHostOptions.Create(
                    parseResult.GetValue(logLevel),
                    parseResult.GetValue(logFormat),
                    parseResult.GetValue(logFile),
                    parseResult.GetValue(metrics));
                using IHost host = await OpenHostAsync(
                    databasePath,
                    hostOptions,
                    cancellationToken).ConfigureAwait(false);
                MixologySession session = host.Services.GetRequiredService<MixologySessionFactory>().Create(principal);
                DashboardResult dashboard = await host.Services.GetRequiredService<DashboardService>()
                    .LoadAsync(session, cancellationToken).ConfigureAwait(false);
                if (dashboard.Error is not null)
                {
                    return await CliErrorAdapter.WriteAsync(error, dashboard.Error).ConfigureAwait(false);
                }

                if (parseResult.GetValue(statusJson))
                {
                    JsonSerializerOptions options = new()
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                    };
                    await output.WriteLineAsync(JsonSerializer.Serialize(dashboard.Data, options)).ConfigureAwait(false);
                    return 0;
                }

                await WriteDashboardAsync(output, dashboard.Data).ConfigureAwait(false);
                return 0;
            }
            catch (Exception exception)
            {
                return await CliErrorAdapter.WriteAsync(error, exception).ConfigureAwait(false);
            }
        });

        root.Subcommands.Add(status);
        root.Subcommands.Add(DrinksCommands.Build(drinksContext));
        root.Subcommands.Add(IngredientsCommands.Build(ingredientsContext));
        root.Subcommands.Add(InventoryCommands.Build(inventoryContext));
        root.Subcommands.Add(MenusCommands.Build(menusContext));
        root.Subcommands.Add(OrdersCommands.Build(ordersContext));
        root.Subcommands.Add(AuditCommands.Build(auditContext));
        root.Subcommands.Add(TagsCommands.Build(tagsContext));
        return root;
    }

    private static async Task WriteDashboardAsync(TextWriter output, DashboardData data)
    {
        await output.WriteLineAsync(
            "DRINKS\tINGREDIENTS\tINVENTORY\tLOW_STOCK\tMENUS\tDRAFT_MENUS\t" +
            "PUBLISHED_MENUS\tORDERS\tPENDING_ORDERS\tAUDIT").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"{data.DrinkCount}\t{data.IngredientCount}\t{data.InventoryCount}\t{data.LowStockCount}\t" +
            $"{data.MenuCount}\t{data.DraftMenus}\t{data.PublishedMenus}\t{data.OrderCount}\t" +
            $"{data.PendingOrders}\t{data.AuditCount}").ConfigureAwait(false);
        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync("RECENT ACTIVITY").ConfigureAwait(false);
        if (data.RecentActivity.Count == 0)
        {
            await output.WriteLineAsync("(none)").ConfigureAwait(false);
            return;
        }

        foreach (DashboardActivity activity in data.RecentActivity)
        {
            await output.WriteLineAsync(
                $"{activity.Timestamp:O}\t{activity.Actor}\t{activity.Action}").ConfigureAwait(false);
        }
    }

    private static bool EnvironmentBoolean(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "0" or "f" or "false" => false,
            "1" or "t" or "true" => true,
            _ => throw AppError.Invalid($"environment variable {name} must be true or false"),
        };
    }

    private static async Task<IHost> OpenHostAsync(
        string databasePath,
        CliHostOptions options,
        CancellationToken cancellationToken)
    {
        options.ValidateLogDestination();
        HostApplicationBuilder builder = MixologyHost.CreateBuilder([]);
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(
            (_, configuration) => options.Configure(configuration),
            preserveStaticLogger: true);
        if (options.Metrics)
        {
            builder.Services.AddOpenTelemetry().WithMetrics(metricsBuilder => metricsBuilder
                .AddMeter("Mixology.Application")
                .AddPrometheusHttpListener(exporter =>
                {
                    exporter.Host = "localhost";
                    exporter.Port = 9090;
                    exporter.ScrapeEndpointPath = "/metrics";
                }));
        }

        builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.AddMixology(databasePath, typeof(MigrationAssemblyMarker).Assembly);
        builder.Services.AddAuditModule();
        builder.Services.AddIngredientsModule();
        builder.Services.AddDrinksModule();
        builder.Services.AddInventoryModule();
        builder.Services.AddMenusModule();
        builder.Services.AddOrdersModule();
        builder.Services.AddTaggingModule();
        builder.Services.AddMixologyPresentation();
        IHost? host = null;
        try
        {
            host = builder.Build();
            await host.Services.GetRequiredService<MixologyStore>()
                .InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            return host;
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            host?.Dispose();
            throw AppError.Internal("open CLI application host", exception);
        }
        catch
        {
            host?.Dispose();
            throw;
        }
    }

    private sealed record CliHostOptions(
        LogEventLevel LogLevel,
        string LogFormat,
        string LogFile,
        bool Metrics)
    {
        private const string TextTemplate =
            "{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

        public static CliHostOptions Create(
            string? level,
            string? format,
            string? file,
            bool metrics)
        {
            string normalizedLevel = level?.Trim().ToLowerInvariant() ?? string.Empty;
            LogEventLevel parsedLevel = normalizedLevel switch
            {
                "debug" => LogEventLevel.Debug,
                "info" => LogEventLevel.Information,
                "warn" or "warning" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                _ => throw AppError.Invalid($"invalid log level \"{level?.Trim()}\""),
            };
            string normalizedFormat = format?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalizedFormat is not ("text" or "json"))
            {
                throw AppError.Invalid($"invalid log format \"{format?.Trim()}\"");
            }

            return new CliHostOptions(parsedLevel, normalizedFormat, file?.Trim() ?? string.Empty, metrics);
        }

        public void ValidateLogDestination()
        {
            if (LogFile.Length == 0)
            {
                return;
            }

            try
            {
                string path = Path.GetFullPath(LogFile);
                if (Directory.Exists(path))
                {
                    throw AppError.Invalid("log file path names a directory");
                }

                using FileStream stream = new(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception exception) when (
                AppError.Find(exception) is null && !AppError.IsCancellation(exception))
            {
                throw AppError.Invalid("log file cannot be opened", exception);
            }
        }

        public void Configure(LoggerConfiguration configuration)
        {
            configuration.MinimumLevel.Is(LogLevel).Enrich.FromLogContext();
            if (LogFormat == "json")
            {
                JsonFormatter formatter = new(renderMessage: true);
                if (LogFile.Length == 0)
                {
                    configuration.WriteTo.Console(
                        formatter,
                        standardErrorFromLevel: LogEventLevel.Verbose);
                }
                else
                {
                    configuration.WriteTo.File(
                        formatter,
                        Path.GetFullPath(LogFile),
                        shared: true);
                }

                return;
            }

            if (LogFile.Length == 0)
            {
                configuration.WriteTo.Console(
                    outputTemplate: TextTemplate,
                    formatProvider: CultureInfo.InvariantCulture,
                    standardErrorFromLevel: LogEventLevel.Verbose);
            }
            else
            {
                configuration.WriteTo.File(
                    Path.GetFullPath(LogFile),
                    outputTemplate: TextTemplate,
                    formatProvider: CultureInfo.InvariantCulture,
                    shared: true);
            }
        }
    }

    private sealed class HostedIngredientsCommandSession(
        IHost host,
        IngredientsModule ingredients,
        MixologySession session) : IIngredientsCommandSession
    {
        public static async ValueTask<IIngredientsCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedIngredientsCommandSession(
                host,
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<Ingredient>> ListAsync(
            ListIngredientsRequest request,
            CancellationToken cancellationToken) =>
            ingredients.ListAsync(session, request, cancellationToken);

        public Task<Ingredient> GetAsync(IngredientId id, CancellationToken cancellationToken) =>
            ingredients.GetAsync(session, id, cancellationToken);

        public Task<Ingredient> CreateAsync(
            CreateIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.CreateAsync(session, request, cancellationToken);

        public Task<Ingredient> UpdateAsync(
            UpdateIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.UpdateAsync(session, request, cancellationToken);

        public Task<Ingredient> RetireAsync(
            RetireIngredientRequest request,
            CancellationToken cancellationToken) =>
            ingredients.RetireAsync(session, request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedAuditCommandSession(
        IHost host,
        AuditModule audit,
        MixologySession session) : IAuditCommandSession
    {
        public static async ValueTask<IAuditCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedAuditCommandSession(
                host,
                host.Services.GetRequiredService<AuditModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<AuditEntry>> ListAsync(
            ListAuditEntriesRequest request,
            CancellationToken cancellationToken) =>
            audit.ListAsync(session, request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedDrinksCommandSession(
        IHost host,
        DrinksModule drinks,
        MixologySession session) : IDrinksCommandSession
    {
        public static async ValueTask<IDrinksCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedDrinksCommandSession(
                host,
                host.Services.GetRequiredService<DrinksModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

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

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedInventoryCommandSession(
        IHost host,
        InventoryModule inventory,
        MixologySession session) : IInventoryCommandSession
    {
        public static async ValueTask<IInventoryCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedInventoryCommandSession(
                host,
                host.Services.GetRequiredService<InventoryModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

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

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedMenusCommandSession(
        IHost host,
        MenusModule menus,
        MixologySession session) : IMenusCommandSession
    {
        public static async ValueTask<IMenusCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedMenusCommandSession(
                host,
                host.Services.GetRequiredService<MenusModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<Page<Menu>> ListAsync(ListMenusRequest request, CancellationToken cancellationToken) =>
            menus.ListAsync(session, request, cancellationToken);

        public Task<Menu> GetAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.GetAsync(session, id, cancellationToken);

        public Task<ReadinessReport> ReadinessAsync(MenuId id, CancellationToken cancellationToken) =>
            menus.ReadinessAsync(session, id, cancellationToken);

        public Task<MenuAnalysis> AnalyzeAsync(
            MenuId id,
            double targetMargin,
            CancellationToken cancellationToken) =>
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

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedOrdersCommandSession(
        IHost host,
        OrdersModule orders,
        MixologySession session) : IOrdersCommandSession
    {
        public static async ValueTask<IOrdersCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedOrdersCommandSession(
                host,
                host.Services.GetRequiredService<OrdersModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

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

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed class HostedTagsCommandSession(
        IHost host,
        TaggingModule tagging,
        MixologySession session) : ITagsCommandSession
    {
        public static async ValueTask<ITagsCommandSession> OpenAsync(
            string databasePath,
            Actor actor,
            CliHostOptions options,
            CancellationToken cancellationToken)
        {
            IHost host = await OpenHostAsync(databasePath, options, cancellationToken).ConfigureAwait(false);
            return new HostedTagsCommandSession(
                host,
                host.Services.GetRequiredService<TaggingModule>(),
                host.Services.GetRequiredService<MixologySessionFactory>().Create(actor));
        }

        public Task<IReadOnlyList<TagReference>> ShowAsync(
            Tag value,
            bool exact,
            CancellationToken cancellationToken) =>
            tagging.ShowAsync(session, value, exact, cancellationToken);

        public Task<IReadOnlyList<TagSummary>> SummaryAsync(CancellationToken cancellationToken) =>
            tagging.SummaryAsync(session, cancellationToken);

        public Task<TagMutationResult> UpsertAsync(
            EntityUid target,
            Tag value,
            CancellationToken cancellationToken) =>
            tagging.UpsertAsync(session, target, value, cancellationToken);

        public Task<TagMutationResult> RemoveAsync(
            EntityUid target,
            string key,
            CancellationToken cancellationToken) =>
            tagging.RemoveAsync(session, target, key, cancellationToken);

        public Task<TagCollection> ListAsync(EntityUid target, CancellationToken cancellationToken) =>
            tagging.ListAsync(session, target, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }
}

public static class CliErrorAdapter
{
    public static async Task<int> WriteAsync(TextWriter error, Exception exception)
    {
        AppError? applicationError = AppError.Find(exception);
        if (applicationError is not null)
        {
            await error.WriteLineAsync(applicationError.UserMessage).ConfigureAwait(false);
            return applicationError.Spec.CliExitCode;
        }

        if (AppError.IsCancellation(exception))
        {
            await error.WriteLineAsync("operation cancelled").ConfigureAwait(false);
            return ErrorCatalog.ExitGeneral;
        }

        await error.WriteLineAsync("internal error").ConfigureAwait(false);
        return ErrorCatalog.ExitGeneral;
    }
}
