using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Inventory.Authorization;
using Mixology.Modules.Inventory.Persistence;
using Mixology.Modules.Inventory.Presentation;
using Mixology.Modules.Inventory.Queries;
using Mixology.Modules.Inventory.Tagging;
using Mixology.Modules.Tagging.Models;
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
        services.TryAddSingleton<InventoryActionProjector>();
        services.TryAddSingleton<InventoryQueries>();
        services.TryAddSingleton<IngredientQueries>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITagTargetRegistrationProvider, InventoryTagTarget>());
        return services;
    }
}
