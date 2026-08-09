using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Application.Auditing;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Audit.Authorization;
using Mixology.Modules.Audit.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, AuditCedarAuthorizationModule>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleModelConfiguration, AuditModelConfiguration>());
        services.TryAddSingleton<AuditModule>();
        services.TryAddSingleton<AuditWriter>();
        services.Replace(ServiceDescriptor.Singleton<IActivityRecorder>(services =>
            services.GetRequiredService<AuditWriter>()));
        return services;
    }
}
