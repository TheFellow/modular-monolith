using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Drinks.Persistence;

public sealed class DrinkModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DrinkRow>(entity =>
        {
            entity.ToTable("drinks");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.Name).HasColumnName("name").IsRequired();
            entity.Property(row => row.Category).HasColumnName("category").IsRequired();
            entity.Property(row => row.Glass).HasColumnName("glass").IsRequired();
            entity.Property(row => row.Garnish).HasColumnName("garnish").IsRequired();
            entity.Property(row => row.Description).HasColumnName("description").IsRequired();
            entity.Property(row => row.Status).HasColumnName("status").IsRequired();
            entity.Property(row => row.DeletedAtUtc).HasColumnName("deleted_at_utc");
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => row.Name).IsUnique();
            entity.HasIndex(row => row.Category);
            entity.HasIndex(row => row.Glass);
            entity.HasIndex(row => row.Status);
            entity.HasMany(row => row.RecipeIngredients)
                .WithOne()
                .HasForeignKey(row => row.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(row => row.RecipeSteps)
                .WithOne()
                .HasForeignKey(row => row.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DrinkRecipeIngredientRow>(entity =>
        {
            entity.ToTable("drink_recipe_ingredients");
            entity.HasKey(row => new { row.DrinkId, row.Position });
            entity.Property(row => row.DrinkId).HasColumnName("drink_id");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.IngredientId).HasColumnName("ingredient_id").IsRequired();
            entity.Property(row => row.Amount).HasColumnName("amount").IsRequired();
            entity.Property(row => row.Unit).HasColumnName("unit").IsRequired();
            entity.Property(row => row.Optional).HasColumnName("optional").IsRequired();
            entity.HasIndex(row => row.IngredientId);
            entity.HasMany(row => row.Substitutes)
                .WithOne()
                .HasForeignKey(row => new { row.DrinkId, row.IngredientPosition })
                .HasPrincipalKey(row => new { row.DrinkId, row.Position })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DrinkRecipeSubstituteRow>(entity =>
        {
            entity.ToTable("drink_recipe_substitutes");
            entity.HasKey(row => new { row.DrinkId, row.IngredientPosition, row.Position });
            entity.Property(row => row.DrinkId).HasColumnName("drink_id");
            entity.Property(row => row.IngredientPosition).HasColumnName("ingredient_position");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.SubstituteId).HasColumnName("substitute_id").IsRequired();
            entity.HasIndex(row => row.SubstituteId);
        });

        modelBuilder.Entity<DrinkRecipeStepRow>(entity =>
        {
            entity.ToTable("drink_recipe_steps");
            entity.HasKey(row => new { row.DrinkId, row.Position });
            entity.Property(row => row.DrinkId).HasColumnName("drink_id");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.Value).HasColumnName("value").IsRequired();
        });
    }
}
