using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Query.Model.Config
{
    public class SqlViewUserConfiguration : IEntityTypeConfiguration<SqlViewUser>
    {
        public void Configure(EntityTypeBuilder<SqlViewUser> builder)
        {
            builder.ToView("vUser").HasNoKey();

            builder.Property(o => o.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint");
            
            builder.Property(o => o.Login)
                .HasColumnName("Login")
                .HasColumnType("nvarchar(64)");
            
            builder.Property(o => o.FirstName)
                .HasColumnName("FirstName")
                .HasColumnType("nvarchar(32)");
            
            builder.Property(o => o.LastName)
                .HasColumnName("LastName")
                .HasColumnType("nvarchar(64)");
            
            builder.Property(o => o.Email)
                .HasColumnName("Email")
                .HasColumnType("nvarchar(128)");
            
            builder.Property(o => o.PasswordHash)
                .HasColumnName("PasswordHash")
                .HasColumnType("varchar(512)");
            
            builder.Property(o => o.PasswordSalt)
                .HasColumnName("PasswordSalt")
                .HasColumnType("varchar(128)");
        }
    }
}
