using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.ExecutionContexts
{
    public class ExecutionContext : IExecutionContext
    {
        public IUserInfo UserInfo { get; }

        public ISystemClock SystemClock { get; }

        public ExecutionContext(IUserInfo userInfo, ISystemClock systemClock)
        {
            UserInfo = userInfo;
            SystemClock = systemClock;
        }
    }
}
