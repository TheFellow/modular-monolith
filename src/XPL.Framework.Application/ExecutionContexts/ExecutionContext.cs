using System;
using XPL.Framework.Domain;

namespace XPL.Framework.Application.ExecutionContexts
{
    public class ExecutionContext : IExecutionContext
    {
        public IUserInfo UserInfo => throw new NotImplementedException();

        public ISystemClock SystemClock => throw new NotImplementedException();
    }
}
