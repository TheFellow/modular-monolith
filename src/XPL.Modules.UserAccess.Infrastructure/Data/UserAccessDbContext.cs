using Microsoft.EntityFrameworkCore;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations.Config;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Config;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public class UserAccessDbContext : DbContext
    {
        private const string _schema = "UserAccess";

        public DbSet<SqlUserRegistration> UserRegistrations { get; set; } = null!;
        public DbSet<SqlUser> Users { get; set; } = null!;

        public UserAccessDbContext(DbContextOptions<UserAccessDbContext> options)
            : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);

            modelBuilder.ApplyConfiguration(new SqlUserRegistrationConfiguration());

            modelBuilder.ApplyConfiguration(new SqlUserConfiguration());
            modelBuilder.ApplyConfiguration(new SqlUserLoginConfiguration());
            modelBuilder.ApplyConfiguration(new SqlUserEmailConfiguration());
        }
    }
}
