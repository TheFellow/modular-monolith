using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Query.Model.Config
{
    public class SqlViewLoginRoleConfig : IEntityTypeConfiguration<SqlViewLoginRole>
    {
        public void Configure(EntityTypeBuilder<SqlViewLoginRole> builder)
        {
            builder.ToView("vLoginRole").HasNoKey();

            builder.Property(o => o.Login)
                .HasColumnName("Login")
                .HasColumnType("nvarchar(64)");
            
            builder.Property(o => o.Role)
                .HasColumnName("Role")
                .HasColumnType("varchar(64)");
        }
    }
}
