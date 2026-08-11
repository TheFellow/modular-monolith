using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Ingredients.Persistence;

public sealed class IngredientModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngredientRow>(entity =>
        {
            entity.ToTable("ingredients");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.Name).HasColumnName("name").IsRequired();
            entity.Property(row => row.Category).HasColumnName("category").IsRequired();
            entity.Property(row => row.Unit).HasColumnName("unit").IsRequired();
            entity.Property(row => row.Description).HasColumnName("description").IsRequired();
            entity.Property(row => row.DeletedAtUtc).HasColumnName("deleted_at_utc");
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => row.Name).IsUnique();
            entity.HasIndex(row => row.Category);
        });
    }
}
