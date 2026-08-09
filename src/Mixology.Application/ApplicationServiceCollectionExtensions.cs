using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Mixology.Application.Auditing;
using Mixology.Application.Events;
using Mixology.Application.Operations;
using Mixology.Persistence;

namespace Mixology.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddMixologyApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDomainEventDispatcher, NullDomainEventDispatcher>();
        services.TryAddSingleton<IActivityRecorder, MissingActivityRecorder>();
        services.AddSingleton<OperationMetrics>();
        services.AddSingleton<SerializationMiddleware>();
        services.AddSingleton<LoggingMiddleware>();
        services.AddSingleton<MetricsMiddleware>();
        services.AddSingleton<TrackActivityMiddleware>();
        services.AddSingleton<UnitOfWorkMiddleware>();
        services.AddSingleton<RecordSuccessfulActivityMiddleware>();
        services.AddSingleton<DispatchEventsMiddleware>();
        services.AddSingleton<OperationPipeline>();
        services.AddSingleton<MixologySessionFactory>();
        return services;
    }

    public static IHostApplicationBuilder AddMixology(
        this IHostApplicationBuilder builder,
        string databasePath)
    {
        builder.Services.AddMixologyPersistence(databasePath);
        builder.Services.AddMixologyApplication();
        return builder;
    }
}
