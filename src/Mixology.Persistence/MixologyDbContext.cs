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
        ModelConfigurationKey = string.Join(
            '|',
            this.configurations.Select(static configuration => configuration.GetType().AssemblyQualifiedName)
                .Order(StringComparer.Ordinal));
    }

    internal string ModelConfigurationKey { get; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareRevisions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareRevisions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (IModuleModelConfiguration configuration in configurations)
        {
            configuration.Configure(modelBuilder);
        }
    }

    private void PrepareRevisions()
    {
        foreach (var entry in ChangeTracker.Entries<IRevisionedRow>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.Revision = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                long expected = entry.Property(row => row.Revision).OriginalValue;
                entry.Entity.Revision = checked(expected + 1);
            }
        }
    }
}
