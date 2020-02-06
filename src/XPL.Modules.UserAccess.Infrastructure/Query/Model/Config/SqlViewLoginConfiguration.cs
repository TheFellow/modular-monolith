using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Query.Model.Config
{
    public class SqlViewLoginConfiguration : IEntityTypeConfiguration<SqlViewLogin>
    {
        public void Configure(EntityTypeBuilder<SqlViewLogin> builder)
        {
            builder.ToView("vLogin").HasNoKey();

            builder.Property(o => o.Login)
                .HasColumnName("Login")
                .HasColumnType("nvarchar(64)");

            builder.Property(o => o.Email)
                .HasColumnName("Email")
                .HasColumnType("nvarchar(128)");
        }
    }
}
