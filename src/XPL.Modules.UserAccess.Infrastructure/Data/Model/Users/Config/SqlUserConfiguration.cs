using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Config
{
    public class SqlUserConfiguration : IEntityTypeConfiguration<SqlUser>
    {
        public void Configure(EntityTypeBuilder<SqlUser> builder)
        {
            builder.ToTable("User")
                .HasKey(o => o.Id);

            builder.Property(o => o.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint")
                .UseHiLo(UserAccessContextOptions.HiLoSequence);

            builder.Property(o => o.RowVersion)
                .IsRowVersion();

            builder.Property(o => o.UserId)
                .HasColumnName("UserId")
                .HasColumnType("uniqueidentifier")
                .IsRequired();

            builder.Property(o => o.Login)
                .HasColumnName("Login")
                .HasColumnType("nvarchar(64)")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(o => o.FirstName)
                .HasColumnName("FirstName")
                .HasColumnType("nvarchar(32)")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(o => o.LastName)
                .HasColumnName("LastName")
                .HasColumnType("nvarchar(64)")
                .HasMaxLength(128)
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

            builder.HasMany(o => o.Emails)
                .WithOne()
                .HasForeignKey(e => e.UserId);

            builder.HasMany(o => o.Logins)
                .WithOne()
                .HasForeignKey(l => l.UserId);
        }
    }
}
