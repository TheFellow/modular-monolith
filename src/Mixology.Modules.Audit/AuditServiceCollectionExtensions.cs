using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Application.Auditing;
using Mixology.Modules.Audit.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleModelConfiguration, AuditModelConfiguration>();
        services.AddSingleton<AuditWriter>();
        services.Replace(ServiceDescriptor.Singleton<IActivityRecorder>(services =>
            services.GetRequiredService<AuditWriter>()));
        return services;
    }
}
