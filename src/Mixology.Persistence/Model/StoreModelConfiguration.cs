using Microsoft.EntityFrameworkCore;

namespace Mixology.Persistence.Model;

internal sealed class StoreModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoreMetadataRow>(entity =>
        {
            entity.ToTable("store_metadata");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.CreatedAtUtc).IsRequired();
        });
    }
}

