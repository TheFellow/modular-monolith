using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mixology.Application;
using Mixology.Application.Events;
using Mixology.Dispatcher;
using Mixology.Kernel.Errors;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Drinks;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Modules.Menus;
using Mixology.Modules.Orders;
using Mixology.Modules.Tagging;
using Mixology.Persistence;

namespace Mixology.Seed;

public static class SeedApplication
{
    public const string DefaultDatabasePath = "data/mixology.db";
    public const string DatabasePathEnvironmentVariable = "MIXOLOGY_DB";

    public static Task<int> RunAsync(
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        string? configured = Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable);
        string databasePath = string.IsNullOrEmpty(configured) ? DefaultDatabasePath : configured;
        return RunAsync(databasePath, output, error, cancellationToken);
    }

    public static async Task<int> RunAsync(
        string databasePath,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        databasePath = string.IsNullOrEmpty(databasePath) ? DefaultDatabasePath : databasePath;
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        IHost? host = null;
        try
        {
            await output.WriteLineAsync("=== Mixology Seed ===").ConfigureAwait(false);
            await output.WriteLineAsync().ConfigureAwait(false);
            SeedDataset dataset = SeedDataset.LoadEmbedded();
            host = await OpenHostAsync(databasePath, cancellationToken).ConfigureAwait(false);
            _ = await host.Services.GetRequiredService<SeedRunner>()
                .RunAsync(dataset, output, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            AppError? applicationError = AppError.Find(exception);
            string message = AppError.IsCancellation(exception)
                ? "operation cancelled"
                : applicationError?.UserMessage ?? AppError.Internal().UserMessage;
            await error.WriteLineAsync($"error: {message}").ConfigureAwait(false);
            return 1;
        }
        finally
        {
            if (host is not null)
            {
                await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
                host.Dispose();
            }
        }
    }

    private static async Task<IHost> OpenHostAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        HostApplicationBuilder builder = MixologyHost.CreateBuilder([]);
        builder.Logging.AddConsole(options =>
            options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.AddMixology(databasePath, typeof(MigrationAssemblyMarker).Assembly);
        builder.Services.AddAuditModule();
        builder.Services.AddIngredientsModule();
        builder.Services.AddDrinksModule();
        builder.Services.AddInventoryModule();
        builder.Services.AddMenusModule();
        builder.Services.AddOrdersModule();
        builder.Services.AddTaggingModule();
        builder.Services.AddSingleton<SeedRunner>();
        IHost host = builder.Build();
        try
        {
            await host.Services.GetRequiredService<MixologyStore>()
                .InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }
}
