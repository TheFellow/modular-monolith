using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public static class UserAccessContextOptions
    {
        public enum TrackingBehavior
        {
            NoTracking,
            TrackAll
        }

        private static readonly ILoggerFactory _myLoggerFactory = LoggerFactory.Create(b => b.AddConsole());

        public static DbContextOptions<UserAccessDbContext> GetOptions(ConnectionString connectionString, TrackingBehavior trackingBehavior) =>
            new DbContextOptionsBuilder<UserAccessDbContext>()
                //.UseLoggerFactory(_myLoggerFactory)
                .UseSqlServer(connectionString.Value)
                .UseQueryTrackingBehavior(trackingBehavior == TrackingBehavior.NoTracking ? QueryTrackingBehavior.NoTracking : QueryTrackingBehavior.TrackAll)
                .EnableSensitiveDataLogging()
                .Options;
    }
}
