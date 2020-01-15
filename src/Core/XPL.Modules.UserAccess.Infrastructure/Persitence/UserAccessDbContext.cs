using Microsoft.EntityFrameworkCore;

namespace XPL.Modules.UserAccess.Infrastructure.Persitence
{
    public class UserAccessDbContext : DbContext
    {
        public UserAccessDbContext(DbContextOptions<UserAccessDbContext> options)
            : base(options)
        {
            
        }
    }
}
