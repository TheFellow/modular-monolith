using Microsoft.EntityFrameworkCore;
using Mixology.Persistence;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Menus.Persistence;

public sealed class MenuModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuRow>(entity =>
        {
            entity.ToTable("menus");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.Name).HasColumnName("name").IsRequired();
            entity.Property(row => row.Description).HasColumnName("description").IsRequired();
            entity.Property(row => row.Status).HasColumnName("status").IsRequired();
            entity.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(row => row.PublishedAtUtc).HasColumnName("published_at_utc");
            entity.Property(row => row.DeletedAtUtc).HasColumnName("deleted_at_utc");
            entity.UseOptimisticConcurrency();
            entity.HasIndex(row => row.Name).IsUnique();
            entity.HasIndex(row => row.Status);
            entity.HasIndex(row => row.CreatedAtUtc);
            entity.HasMany(row => row.Items)
                .WithOne()
                .HasForeignKey(row => row.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuItemRow>(entity =>
        {
            entity.ToTable("menu_items");
            entity.HasKey(row => new { row.MenuId, row.DrinkId });
            entity.Property(row => row.MenuId).HasColumnName("menu_id");
            entity.Property(row => row.DrinkId).HasColumnName("drink_id");
            entity.Property(row => row.DisplayName).HasColumnName("display_name");
            entity.Property(row => row.PriceAmount).HasColumnName("price_amount");
            entity.Property(row => row.PriceCurrency).HasColumnName("price_currency");
            entity.Property(row => row.Featured).HasColumnName("featured").IsRequired();
            entity.Property(row => row.Availability).HasColumnName("availability").IsRequired();
            entity.Property(row => row.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.HasIndex(row => row.DrinkId);
            entity.HasIndex(row => new { row.MenuId, row.SortOrder }).IsUnique();
        });
    }
}
