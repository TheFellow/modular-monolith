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
using Mixology.Presentation;
using OpenTelemetry.Metrics;
using Serilog;

namespace Mixology.Desktop;

public sealed class DesktopHost : IAsyncDisposable
{
    private readonly IHost host;

    private DesktopHost(IHost host) => this.host = host;

    public IServiceProvider Services => host.Services;

    public static async Task<DesktopHost> OpenAsync(
        DesktopOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ValidateLogDestination();
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
            return new DesktopHost(host);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            host?.Dispose();
            throw AppError.Internal("open desktop application host", exception);
        }
        catch
        {
            host?.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            AppError.Find(exception) is null && !AppError.IsCancellation(exception))
        {
            throw AppError.Internal("close desktop application host", exception);
        }
        finally
        {
            host.Dispose();
        }
    }
}
