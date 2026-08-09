using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Authorization.Cedar;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Persistence;
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
        return services;
    }
}
