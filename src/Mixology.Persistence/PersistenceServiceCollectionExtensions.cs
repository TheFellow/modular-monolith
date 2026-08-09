using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mixology.Persistence.Model;

namespace Mixology.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMixologyPersistence(
        this IServiceCollection services,
        string databasePath,
        Assembly migrationsAssembly)
    {
        ArgumentNullException.ThrowIfNull(migrationsAssembly);
        StoreSettings settings = new(databasePath);
        string migrationsAssemblyName = migrationsAssembly.GetName().Name
            ?? throw new InvalidOperationException("Migration assembly has no name.");
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IModuleModelConfiguration, StoreModelConfiguration>();
        services.AddDbContextFactory<MixologyDbContext>(options => options.UseSqlite(
            settings.ConnectionString,
            sqlite => sqlite.MigrationsAssembly(migrationsAssemblyName)));
        services.AddSingleton<MixologyStore>();
        return services;
    }
}
