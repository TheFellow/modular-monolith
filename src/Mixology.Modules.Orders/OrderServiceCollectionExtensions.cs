using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Menus.Queries;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Persistence;
using Mixology.Modules.Orders.Queries;
using Mixology.Modules.Orders.Tagging;
using Mixology.Modules.Tagging.Models;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Orders;

public static class OrderServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddCedarAuthorization();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICedarAuthorizationModule, OrderCedarAuthorizationModule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleModelConfiguration, OrderModelConfiguration>());
        services.TryAddSingleton<OrdersModule>();
        services.TryAddSingleton<OrderQueries>();
        services.TryAddSingleton<MenuQueries>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITagTargetRegistrationProvider, OrderTagTarget>());
        return services;
    }
}
