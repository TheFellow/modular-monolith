using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Ingredients.Authorization;
using Mixology.Modules.Ingredients.Persistence;
using Mixology.Modules.Ingredients.Presentation;
using Mixology.Modules.Ingredients.Queries;
using Mixology.Modules.Ingredients.Tagging;
using Mixology.Modules.Tagging.Models;
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
        services.TryAddSingleton<IngredientActionProjector>();
        services.TryAddSingleton<IngredientQueries>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITagTargetRegistrationProvider, IngredientTagTarget>());
        return services;
    }
}
