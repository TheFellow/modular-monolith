using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Config
{
    public class SqlUserLoginConfiguration : IEntityTypeConfiguration<SqlUserLogin>
    {
        public void Configure(EntityTypeBuilder<SqlUserLogin> builder)
        {
            builder.ToTable("UserLogin")
                .HasKey(o => o.Id);

            builder.Property(o => o.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint")
                .UseHiLo(UserAccessContextOptions.HiLoSequence);

            builder.Property(o => o.UserId)
                .HasColumnName("UserId")
                .HasColumnType("bigint")
                .IsRequired();

            builder.Property(o => o.PasswordHash)
                .HasColumnName("PasswordHash")
                .HasColumnType("varchar(512)")
                .HasMaxLength(512)
                .IsRequired();

            builder.Property(o => o.PasswordSalt)
                .HasColumnName("PasswordSalt")
                .HasColumnType("varchar(128)")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(o => o.BeginOnUtc)
                .HasColumnName("BeginOnUtc")
                .HasColumnType("datetime2")
                .IsRequired();

            builder.Property(o => o.EndOnUtc)
                .HasColumnName("EndOnUtc")
                .HasColumnType("datetime2");

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
