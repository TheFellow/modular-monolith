using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XPL.Modules.UserAccess.Infrastructure.Query.Model.Config
{
    public class SqlLoginViewConfiguration : IEntityTypeConfiguration<SqlLoginView>
    {
        public void Configure(EntityTypeBuilder<SqlLoginView> builder)
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
