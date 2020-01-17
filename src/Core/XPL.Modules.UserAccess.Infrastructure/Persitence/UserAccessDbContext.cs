using Microsoft.EntityFrameworkCore;
using XPL.Modules.UserAccess.Infrastructure.Persitence.Model;
using XPL.Modules.UserAccess.Infrastructure.Persitence.Model.Configurations;

namespace XPL.Modules.UserAccess.Infrastructure.Persitence
{
    public class UserAccessDbContext : DbContext
    {
        private const string _schema = "UserAccess";

        public DbSet<SqlUserRegistration> UserRegistrations { get; set; } = null!;

        public UserAccessDbContext(DbContextOptions<UserAccessDbContext> options)
            : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);

            modelBuilder.ApplyConfiguration(new SqlUserRegistrationConfiguration());
        }
    }
}
