using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public static class UserAccessContextOptions
    {
        private static readonly ILoggerFactory _myLoggerFactory = LoggerFactory.Create(b => b.AddConsole());

        public static DbContextOptions<UserAccessDbContext> GetOptions(ConnectionString connectionString) =>
            new DbContextOptionsBuilder<UserAccessDbContext>()
                .UseLoggerFactory(_myLoggerFactory)
                .UseSqlServer(connectionString.Value)
                .EnableSensitiveDataLogging()
                .Options;
    }
}
