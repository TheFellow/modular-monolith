using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Config
{
    public class SqlUserRoleConfiguration : IEntityTypeConfiguration<SqlUserRole>
    {
        public void Configure(EntityTypeBuilder<SqlUserRole> builder)
        {
            builder.ToTable("UserRole")
                .HasKey(o => o.Id);

            builder.Property(o => o.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint");

            builder.Property(o => o.UserId)
                   .HasColumnName("UserId")
                   .HasColumnType("bigint")
                   .IsRequired();

            builder.Property(o => o.Role)
                .HasColumnName("Role")
                .HasColumnType("varchar(64)")
                .HasMaxLength(64)
                .IsRequired();
            
            builder.Property(o => o.BeginOnUtc)
                .HasColumnName("BeginOnUtc")
                .HasColumnType("datetime2")
                .IsRequired();
            
            builder.Property(o => o.EndOnUtc)
                .HasColumnName("EndOnUtc")
                .HasColumnType("datetime2")
                .IsRequired();
            
            builder.Property(o => o.UpdatedBy)
                .HasColumnName("UpdatedBy")
                .HasColumnType("varchar(32)")
                .HasMaxLength(32)
                .IsRequired();
            
            builder.Property(o => o.UpdatedOn)
                .HasColumnName("UpdatedOn")
                .HasColumnType("datetime2")
                .IsRequired();
        }
    }
}
