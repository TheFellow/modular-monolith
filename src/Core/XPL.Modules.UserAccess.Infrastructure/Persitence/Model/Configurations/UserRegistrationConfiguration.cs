using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Persitence.Model.Configurations
{
    public class UserRegistrationConfiguration : IEntityTypeConfiguration<UserRegistrationSql>
    {
        private const string _schema = "UserAccess";
        private const string _sequence = "SeqPrimaryKeys";

        public void Configure(EntityTypeBuilder<UserRegistrationSql> builder)
        {
            builder.ToTable(_schema)
                .HasKey(u => u.Id);

            builder.Property(u => u.Id)
                .UseHiLo(_sequence, _schema);

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
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(u => u.PasswordSalt)
                .HasColumnName("PasswordSalt")
                .HasColumnType("varchar(12)")
                .HasMaxLength(12)
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
                .IsRequired()
                .IsConcurrencyToken();
        }
    }
}
