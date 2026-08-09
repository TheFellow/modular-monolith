using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Drinks.Authorization;
using Mixology.Modules.Drinks.Persistence;
using Mixology.Modules.Drinks.Queries;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Drinks;

public static class DrinkServiceCollectionExtensions
{
    public static IServiceCollection AddDrinksModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, DrinkCedarAuthorizationModule>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleModelConfiguration, DrinkModelConfiguration>());
        services.TryAddSingleton<DrinksModule>();
        services.TryAddSingleton<DrinkQueries>();
        return services;
    }
}
