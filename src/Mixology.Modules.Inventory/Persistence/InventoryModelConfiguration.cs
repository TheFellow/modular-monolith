using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Inventory.Persistence;

public sealed class InventoryModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryRow>(entity =>
        {
            entity.ToTable("inventory_stock");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.IngredientId).HasColumnName("ingredient_id").IsRequired();
            entity.Property(row => row.Quantity).HasColumnName("quantity");
            entity.Property(row => row.Unit).HasColumnName("unit").IsRequired();
            entity.Property(row => row.UnitCostAmount).HasColumnName("unit_cost_amount");
            entity.Property(row => row.UnitCostCurrency).HasColumnName("unit_cost_currency");
            entity.Property(row => row.LastUpdatedUtc).HasColumnName("last_updated_utc");
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => row.IngredientId).IsUnique();
            entity.HasIndex(row => row.LastUpdatedUtc);
        });

        modelBuilder.Entity<InventoryReservationRow>(entity =>
        {
            entity.ToTable("inventory_reservations");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(row => row.IngredientId).HasColumnName("ingredient_id").IsRequired();
            entity.Property(row => row.Quantity).HasColumnName("quantity");
            entity.Property(row => row.Unit).HasColumnName("unit").IsRequired();
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => row.OrderId);
            entity.HasIndex(row => row.IngredientId);
            entity.HasOne<InventoryRow>()
                .WithMany()
                .HasForeignKey(row => row.IngredientId)
                .HasPrincipalKey(row => row.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
