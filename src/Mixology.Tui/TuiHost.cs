using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Dispatcher;
using Mixology.Kernel.Errors;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Presentation;
using Mixology.Modules.Drinks;
using Mixology.Modules.Drinks.Presentation;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Inventory;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Menus;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Orders;
using Mixology.Modules.Orders.Presentation;
using Mixology.Modules.Tagging;
using Mixology.Modules.Tagging.Presentation;
using Mixology.Persistence;
using Mixology.Presentation;
using Mixology.Presentation.Dashboard;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;
using Mixology.Tui.Workspaces;
using Mixology.Tui.Workspaces.Audit;
using Mixology.Tui.Workspaces.Drinks;
using Mixology.Tui.Workspaces.Menus;
using Mixology.Tui.Workspaces.Orders;
using Mixology.Tui.Workspaces.Tags;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Mixology.Tui;

public sealed record TuiOptions(
    string DatabasePath,
    Actor Actor,
    LogEventLevel LogLevel,
    string LogFormat,
    string LogFile,
    bool Metrics)
{
    private const string TextTemplate =
        "{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static TuiOptions Create(
        string? databasePath,
        string? actor,
        string? logLevel,
        string? logFormat,
        string? logFile,
        bool metrics)
    {
        string database = Path.GetFullPath(
            string.IsNullOrWhiteSpace(databasePath)
                ? Path.Combine("data", "mixology.db")
                : databasePath);
        string level = logLevel?.Trim().ToLowerInvariant() ?? string.Empty;
        LogEventLevel parsedLevel = level switch
        {
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warn" or "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            _ => throw AppError.Invalid($"invalid log level \"{logLevel?.Trim()}\""),
        };
        string format = logFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        if (format is not ("text" or "json"))
        {
            throw AppError.Invalid($"invalid log format \"{logFormat?.Trim()}\"");
        }

        string destination = string.IsNullOrWhiteSpace(logFile)
            ? DefaultLogPath(database)
            : Path.GetFullPath(logFile.Trim());
        if (Directory.Exists(destination))
        {
            throw AppError.Invalid("log file path names a directory");
        }

        return new TuiOptions(database, Actor.Parse(actor), parsedLevel, format, destination, metrics);
    }

    public static string DefaultLogPath(string databasePath)
    {
        string full = Path.GetFullPath(databasePath);
        string? directory = Path.GetDirectoryName(full);
        return Path.Combine(directory ?? string.Empty, "mixology-tui.log");
    }

    public void Configure(LoggerConfiguration configuration)
    {
        _ = configuration.MinimumLevel.Is(LogLevel).Enrich.FromLogContext();
        string? directory = Path.GetDirectoryName(LogFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (string.Equals(LogFormat, "json", StringComparison.Ordinal))
        {
            _ = configuration.WriteTo.File(new JsonFormatter(), LogFile, shared: true);
        }
        else
        {
            _ = configuration.WriteTo.File(
                LogFile,
                outputTemplate: TextTemplate,
                formatProvider: CultureInfo.InvariantCulture,
                shared: true);
        }
    }
}

public interface ITuiRuntime
{
    Task RunAsync(TuiOptions options, CancellationToken cancellationToken = default);
}

public sealed class HostedTuiRuntime(ITuiRunner? runner = null) : ITuiRuntime
{
    private readonly ITuiRunner runner = runner ?? new TerminalGuiRunner();

    public async Task RunAsync(TuiOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await using TuiHost host = await TuiHost.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        MixologySession session = host.Services.GetRequiredService<MixologySessionFactory>().Create(options.Actor);
        DashboardService dashboard = host.Services.GetRequiredService<DashboardService>();
        NavigationProjection navigation = await host.Services.GetRequiredService<NavigationProjector>()
            .ProjectAsync(options.Actor, cancellationToken).ConfigureAwait(false);
        Dictionary<WorkspaceId, Func<ITuiWorkspace>> workspaces = new()
        {
            [TuiRoutes.Dashboard.Id] = () => new DashboardWorkspace(
                token => dashboard.LoadAsync(session, token)),
            [TuiRoutes.Drinks.Id] = DrinksWorkspace.CreateFactory(
                host.Services.GetRequiredService<DrinksModule>(),
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<DrinkActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                options.Actor),
            [TuiRoutes.Ingredients.Id] = IngredientsWorkspace.CreateFactory(
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<IngredientActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                options.Actor),
            [TuiRoutes.Inventory.Id] = InventoryWorkspace.CreateFactory(
                host.Services.GetRequiredService<InventoryModule>(),
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<InventoryActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                options.Actor),
            [TuiRoutes.Menus.Id] = MenusWorkspace.CreateFactory(
                host.Services.GetRequiredService<MenusModule>(),
                host.Services.GetRequiredService<DrinksModule>(),
                host.Services.GetRequiredService<MenuActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                options.Actor),
            [TuiRoutes.Orders.Id] = OrdersWorkspace.CreateFactory(
                host.Services.GetRequiredService<OrdersModule>(),
                host.Services.GetRequiredService<MenusModule>(),
                host.Services.GetRequiredService<DrinksModule>(),
                host.Services.GetRequiredService<OrderActionProjector>(),
                host.Services.GetRequiredService<TaggedMutationCoordinator>(),
                session,
                options.Actor),
            [TuiRoutes.Audit.Id] = AuditWorkspace.CreateFactory(
                host.Services.GetRequiredService<AuditModule>(),
                host.Services.GetRequiredService<AuditActionProjector>(),
                session,
                options.Actor),
            [TuiRoutes.Tags.Id] = TagsWorkspace.CreateFactory(
                host.Services.GetRequiredService<TaggingModule>(),
                host.Services.GetRequiredService<TaggingActionProjector>(),
                host.Services.GetRequiredService<DrinksModule>(),
                host.Services.GetRequiredService<IngredientsModule>(),
                host.Services.GetRequiredService<InventoryModule>(),
                host.Services.GetRequiredService<MenusModule>(),
                host.Services.GetRequiredService<OrdersModule>(),
                session,
                options.Actor),
        };
        await using TuiShell shell = new(navigation, workspaces);
        await shell.StartAsync(cancellationToken).ConfigureAwait(false);
        await runner.RunAsync(shell, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class TuiHost : IAsyncDisposable
{
    private readonly IHost host;

    private TuiHost(IHost host) => this.host = host;

    public IServiceProvider Services => host.Services;

    public static async Task<TuiHost> OpenAsync(
        TuiOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        HostApplicationBuilder builder = MixologyHost.CreateBuilder([]);
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, configuration) => options.Configure(configuration));
        if (options.Metrics)
        {
            builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
                .AddMeter("Mixology.Application")
                .AddPrometheusHttpListener(exporter =>
                {
                    exporter.Host = "localhost";
                    exporter.Port = 9090;
                    exporter.ScrapeEndpointPath = "/metrics";
                }));
        }

        builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.AddMixology(options.DatabasePath, typeof(MigrationAssemblyMarker).Assembly);
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
                .InitializeAsync(cancellationToken).ConfigureAwait(false);
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            return new TuiHost(host);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            host?.Dispose();
            throw AppError.Internal("open TUI application host", exception);
        }
        catch
        {
            host?.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        host.Dispose();
    }
}
