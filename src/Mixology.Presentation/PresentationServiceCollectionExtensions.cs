using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Presentation.Dashboard;
using Mixology.Presentation.Mutations;
using Mixology.Presentation.Navigation;

namespace Mixology.Presentation;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddMixologyPresentation(this IServiceCollection services)
    {
        services.TryAddSingleton<ModuleDashboardDataSourceFactory>();
        services.TryAddSingleton<DashboardService>();
        services.TryAddSingleton<NavigationProjector>();
        services.TryAddSingleton<TaggedMutationCoordinator>();
        return services;
    }
}
