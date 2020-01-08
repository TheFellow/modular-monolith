﻿using XPL.Framework.Application.Ports;
using XPL.Framework.Modules.Startup;

namespace XPL.Modules.UserAccess.Application.Startup
{
    public class TestOnStartup : IRunOnStartup
    {
        private readonly ILogger _logger;

        public TestOnStartup(ILogger logger) => _logger = logger;
        public void Execute() => _logger.Info($"Executing {GetType().FullName}");
    }
}