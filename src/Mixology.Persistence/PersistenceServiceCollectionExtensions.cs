using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Persistence.Model;

namespace Mixology.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMixologyPersistence(this IServiceCollection services, string databasePath)
    {
        StoreSettings settings = new(databasePath);
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IModuleModelConfiguration, StoreModelConfiguration>();
        services.AddDbContextFactory<MixologyDbContext>(options =>
            options.UseSqlite(settings.ConnectionString));
        services.AddSingleton<MixologyStore>();
        return services;
    }
}
