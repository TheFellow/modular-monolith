using Microsoft.EntityFrameworkCore;
using Mixology.Persistence.Model;

namespace Mixology.Modules.Audit.Persistence;

public sealed class AuditModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntryRow>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.Action).HasColumnName("action").IsRequired();
            entity.Property(row => row.ResourceType).HasColumnName("resource_type");
            entity.Property(row => row.ResourceId).HasColumnName("resource_id");
            entity.Property(row => row.PrincipalId).HasColumnName("principal_id").IsRequired();
            entity.Property(row => row.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
            entity.Property(row => row.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired();
            entity.Property(row => row.Success).HasColumnName("success").IsRequired();
            entity.Property(row => row.ErrorKind).HasColumnName("error_kind");
            entity.Property(row => row.Error).HasColumnName("error");
            entity.HasIndex(row => row.Action);
            entity.HasIndex(row => new { row.ResourceType, row.ResourceId });
            entity.HasIndex(row => row.PrincipalId);
            entity.HasIndex(row => row.StartedAtUtc);
            entity.HasIndex(row => row.Success);
            entity.HasMany(row => row.Touches)
                .WithOne()
                .HasForeignKey(touch => touch.AuditEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditTouchRow>(entity =>
        {
            entity.ToTable("audit_touches");
            entity.HasKey(row => new { row.AuditEntryId, row.Position });
            entity.Property(row => row.AuditEntryId).HasColumnName("audit_entry_id").IsRequired();
            entity.Property(row => row.Position).HasColumnName("position").IsRequired();
            entity.Property(row => row.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(row => row.EntityId).HasColumnName("entity_id").IsRequired();
            entity.HasIndex(row => new { row.EntityType, row.EntityId });
        });
    }
}
