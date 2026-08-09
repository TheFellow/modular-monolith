using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mixology.Authorization.Cedar;

public static class CedarServiceCollectionExtensions
{
    public static IServiceCollection AddCedarAuthorization(this IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, OwnerCedarAuthorizationModule>());
        services.TryAddSingleton<IEntityAuthorizer, CedarAuthorizer>();
        return services;
    }
}
