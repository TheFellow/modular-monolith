using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Inventory.Authorization;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Inventory;

public static class InventoryServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, InventoryCedarAuthorizationModule>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleModelConfiguration, InventoryModelConfiguration>());
        services.TryAddSingleton<InventoryModule>();
        return services;
    }
}
