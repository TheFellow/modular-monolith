using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Orders.Persistence;

public sealed class OrderModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderRow>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.MenuId).HasColumnName("menu_id");
            entity.Property(row => row.Status).HasColumnName("status");
            entity.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(row => row.CompletedAtUtc).HasColumnName("completed_at_utc");
            entity.Property(row => row.Notes).HasColumnName("notes");
            entity.Property(row => row.DeletedAtUtc).HasColumnName("deleted_at_utc");
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => row.MenuId);
            entity.HasIndex(row => row.Status);
            entity.HasIndex(row => row.CreatedAtUtc);
            entity.HasMany(row => row.Items)
                .WithOne()
                .HasForeignKey(row => row.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(row => row.IngredientUsage)
                .WithOne()
                .HasForeignKey(row => row.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(row => row.BlockedIngredients)
                .WithOne()
                .HasForeignKey(row => row.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderItemRow>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(row => new { row.OrderId, row.Position });
            entity.Property(row => row.OrderId).HasColumnName("order_id");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.DrinkId).HasColumnName("drink_id");
            entity.Property(row => row.Quantity).HasColumnName("quantity");
            entity.Property(row => row.Notes).HasColumnName("notes");
            entity.HasIndex(row => row.DrinkId);
        });
        modelBuilder.Entity<OrderIngredientUsageRow>(entity =>
        {
            entity.ToTable("order_ingredient_usage");
            entity.HasKey(row => new { row.OrderId, row.Position });
            entity.Property(row => row.OrderId).HasColumnName("order_id");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.IngredientId).HasColumnName("ingredient_id");
            entity.Property(row => row.Name).HasColumnName("name");
            entity.Property(row => row.Quantity).HasColumnName("quantity");
            entity.Property(row => row.Unit).HasColumnName("unit");
            entity.HasIndex(row => row.IngredientId);
        });
        modelBuilder.Entity<OrderBlockedIngredientRow>(entity =>
        {
            entity.ToTable("order_blocked_ingredients");
            entity.HasKey(row => new { row.OrderId, row.IngredientId });
            entity.Property(row => row.OrderId).HasColumnName("order_id");
            entity.Property(row => row.IngredientId).HasColumnName("ingredient_id");
            entity.HasIndex(row => row.IngredientId);
        });
    }
}
