using Microsoft.EntityFrameworkCore;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public static class UserAccessContextOptions
    {
        public static DbContextOptions<UserAccessDbContext> GetOptions(ConnectionString connectionString) =>
            new DbContextOptionsBuilder<UserAccessDbContext>()
                .UseSqlServer(connectionString.Value)
                .Options;
    }
}
