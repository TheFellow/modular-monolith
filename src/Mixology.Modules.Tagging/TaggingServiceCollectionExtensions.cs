using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Tagging.Authorization;
using Mixology.Modules.Tagging.Models;
using Mixology.Modules.Tagging.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Tagging;

public static class TaggingServiceCollectionExtensions
{
    public static IServiceCollection AddTaggingModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICedarAuthorizationModule, TaggingCedarAuthorizationModule>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleModelConfiguration, TaggingModelConfiguration>());
        services.TryAddSingleton<TagTargetRegistry>();
        services.TryAddSingleton<TagRepository>();
        services.TryAddSingleton<ITagReader>(static provider => provider.GetRequiredService<TagRepository>());
        services.TryAddSingleton<TaggingModule>();
        return services;
    }
}
