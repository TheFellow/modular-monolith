using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Tagging.Persistence;

public sealed class TaggingModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TagAssociationRow>(entity =>
        {
            entity.ToTable("entity_tags");
            entity.HasKey(row => new { row.EntityType, row.EntityId, row.Key });
            entity.Property(row => row.EntityType)
                .HasColumnName("entity_type")
                .UseCollation("BINARY")
                .IsRequired();
            entity.Property(row => row.EntityId)
                .HasColumnName("entity_id")
                .UseCollation("BINARY")
                .IsRequired();
            entity.Property(row => row.Key)
                .HasColumnName("key")
                .UseCollation("BINARY")
                .IsRequired();
            entity.Property(row => row.Value)
                .HasColumnName("value")
                .UseCollation("BINARY")
                .IsRequired();
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => new { row.EntityType, row.EntityId });
            entity.HasIndex(row => new { row.Key, row.Value });
        });
    }
}
