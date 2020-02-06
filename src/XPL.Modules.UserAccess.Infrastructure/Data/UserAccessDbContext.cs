using Microsoft.EntityFrameworkCore;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations.Config;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Config;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public class UserAccessDbContext : DbContext
    {
        private const string _schema = "UserAccess";
        private const string _hiLoSequence = "SeqPrimaryKeys";

        public DbSet<SqlRegistration> Registrations { get; set; } = null!;
        public DbSet<SqlUser> Users { get; set; } = null!;

        public UserAccessDbContext(DbContextOptions<UserAccessDbContext> options)
            : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);

            modelBuilder.UseHiLo(_hiLoSequence, _schema);

            modelBuilder.ApplyConfiguration(new SqlRegistrationConfiguration());

            modelBuilder.ApplyConfiguration(new SqlUserConfiguration());
            modelBuilder.ApplyConfiguration(new SqlUserPasswordConfiguration());
            modelBuilder.ApplyConfiguration(new SqlUserEmailConfiguration());
            modelBuilder.ApplyConfiguration(new SqlUserRoleConfiguration());
        }
    }
}
