using Microsoft.EntityFrameworkCore;
using Mixology.Persistence.Model;

namespace Mixology.Persistence;

public sealed class MixologyDbContext : DbContext
{
    private readonly IReadOnlyList<IModuleModelConfiguration> configurations;

    public MixologyDbContext(
        DbContextOptions<MixologyDbContext> options,
        IEnumerable<IModuleModelConfiguration> configurations)
        : base(options)
    {
        this.configurations = configurations.ToArray();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (IModuleModelConfiguration configuration in configurations)
        {
            configuration.Configure(modelBuilder);
        }
    }
}

