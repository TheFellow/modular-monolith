using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations.Config
{
    public class SqlRegistrationConfiguration : IEntityTypeConfiguration<SqlRegistration>
    {
        public void Configure(EntityTypeBuilder<SqlRegistration> builder)
        {
            builder.ToTable("Registration")
                .HasKey(u => u.Id);

            builder.Property(u => u.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint");

            builder.Property(u => u.RowVersion)
                .IsRowVersion();

            builder.Property(u => u.RegistrationId)
                .HasColumnName("RegistrationId")
                .IsRequired();

            builder.Property(u => u.Login)
                .HasColumnName("Login")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(u => u.ConfirmationCode)
                .HasColumnName("ConfirmationCode")
                .HasColumnType("varchar(16)")
                .HasMaxLength(16)
                .IsRequired();

            builder.Property(u => u.ExpiryDate)
                .HasColumnName("ExpiryDate")
                .HasColumnType("date")
                .IsRequired();

            builder.Property(u => u.PasswordHash)
                .HasColumnName("PasswordHash")
                .HasColumnType("varchar(512)")
                .HasMaxLength(512)
                .IsRequired();

            builder.Property(u => u.PasswordSalt)
                .HasColumnName("PasswordSalt")
                .HasColumnType("varchar(128)")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(u => u.FirstName)
                .HasColumnName("FirstName")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(u => u.LastName)
                .HasColumnName("LastName")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(u => u.Email)
                .HasColumnName("Email")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(u => u.Status)
                .HasColumnName("Status")
                .HasColumnType("varchar(32)")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(u => u.StatusDate)
                .HasColumnName("StatusDate")
                .HasColumnType("date")
                .IsRequired();

            builder.Property(u => u.UpdatedBy)
                .HasColumnName("UpdatedBy")
                .HasColumnType("varchar(32)")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(u => u.UpdatedOn)
                .HasColumnName("UpdatedOn")
                .IsRequired();
        }
    }
}
