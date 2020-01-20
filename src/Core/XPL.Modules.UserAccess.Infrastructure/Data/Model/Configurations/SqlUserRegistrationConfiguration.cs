using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Configurations
{
    public class SqlUserRegistrationConfiguration : IEntityTypeConfiguration<SqlUserRegistration>
    {
        private const string _sequence = "SeqPrimaryKeys";

        public void Configure(EntityTypeBuilder<SqlUserRegistration> builder)
        {
            builder.ToTable("UserRegistration")
                .HasKey(u => u.Id);

            builder.Property(u => u.Id)
                .UseHiLo(_sequence);

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

            builder.Property(u => u.PasswordHash)
                .HasColumnName("PasswordHash")
                .HasMaxLength(512)
                .IsRequired();

            builder.Property(u => u.PasswordSalt)
                .HasColumnName("PasswordSalt")
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

            builder.Property(u => u.UpdatedBy)
                .HasColumnName("UpdatedBy")
                .HasColumnType("varchar(32)")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(u => u.UpdatedOn)
                .HasColumnName("UpdatedOn")
                .IsRequired();

            builder.Property(u => u.RowVersion)
                .IsConcurrencyToken();
        }
    }
}
