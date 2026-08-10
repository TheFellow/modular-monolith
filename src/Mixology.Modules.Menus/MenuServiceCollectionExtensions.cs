using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Drinks.Queries;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Inventory.Queries;
using Mixology.Modules.Menus.Authorization;
using Mixology.Modules.Menus.Persistence;
using Mixology.Modules.Menus.Ports;
using Mixology.Modules.Menus.Presentation;
using Mixology.Modules.Menus.Queries;
using Mixology.Modules.Menus.Tagging;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Menus;

public static class MenuServiceCollectionExtensions
{
    public static IServiceCollection AddMenusModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, MenuCedarAuthorizationModule>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleModelConfiguration, MenuModelConfiguration>());
        services.TryAddSingleton<DrinkQueries>();
        services.TryAddSingleton<IngredientQueries>();
        services.TryAddSingleton<InventoryQueries>();
        services.TryAddSingleton<IMenuOperations, MenuOperations>();
        services.TryAddSingleton<MenuQueries>();
        services.TryAddSingleton<MenusModule>();
        services.TryAddSingleton<MenuActionProjector>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITagTargetRegistrationProvider, MenuTagTarget>());
        return services;
    }
}
