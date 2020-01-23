using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Config
{
    public class SqlUserEmailConfiguration : IEntityTypeConfiguration<SqlUserEmail>
    {
        public void Configure(EntityTypeBuilder<SqlUserEmail> builder)
        {
            builder.ToTable("UserEmail")
                .HasKey(o => o.Id);

            builder.Property(o => o.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint")
                .UseHiLo(UserAccessContextOptions.HiLoSequence);

            builder.Property(o => o.UserId)
                .HasColumnName("UserId")
                .HasColumnType("bigint")
                .IsRequired();

            builder.Property(o => o.Email)
                .HasColumnName("Email")
                .HasColumnType("nvarchar(128)")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(o => o.Status)
                .HasColumnName("Status")
                .HasColumnType("varchar(32)")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(o => o.StatusDate)
                .HasColumnName("StatusDate")
                .HasColumnType("date")
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
