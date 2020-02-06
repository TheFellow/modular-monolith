using Microsoft.EntityFrameworkCore;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Infrastructure.Query.Model;
using XPL.Modules.UserAccess.Infrastructure.Query.Model.Config;

namespace XPL.Modules.UserAccess.Infrastructure.Query
{
    public class UserAccessQueryContext : DbContext
    {
        private const string _schema = "UserAccess";
        private readonly ConnectionString _connectionString;

        public DbSet<SqlUserView> Users { get; set; } = null!;
        public DbSet<SqlLoginView> Logins { get; set; } = null!;

        public UserAccessQueryContext(ConnectionString connectionString) => _connectionString = connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer(_connectionString.Value);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);

            modelBuilder.ApplyConfiguration(new SqlUserViewConfiguration());
            modelBuilder.ApplyConfiguration(new SqlLoginViewConfiguration());
        }
    }
}
