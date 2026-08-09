using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Modules.Audit;
using Mixology.Modules.Ingredients;
using Mixology.Modules.Inventory;
using Mixology.Persistence;

namespace Mixology.Migrations;

public sealed class MixologyDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MixologyDbContext>
{
    public MixologyDbContext CreateDbContext(string[] args)
    {
        _ = args;
        ServiceCollection services = new();
        services.AddMixologyPersistence(
            Path.Combine(Path.GetTempPath(), "mixology-design.db"),
            typeof(MigrationAssemblyMarker).Assembly);
        services.AddAuditModule();
        services.AddIngredientsModule();
        services.AddInventoryModule();
        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDbContextFactory<MixologyDbContext>>().CreateDbContext();
    }
}
