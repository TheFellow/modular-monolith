using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Ingredients;

public static class IngredientServiceCollectionExtensions
{
    public static IServiceCollection AddIngredientsModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, IngredientCedarAuthorizationModule>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleModelConfiguration, IngredientModelConfiguration>());
        services.TryAddSingleton<IngredientsModule>();
        return services;
    }
}
