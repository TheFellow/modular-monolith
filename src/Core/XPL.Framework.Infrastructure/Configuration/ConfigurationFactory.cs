using Microsoft.Extensions.Configuration;
using System;

namespace XPL.Framework.Infrastructure.Configuration
{
    public static class ConfigurationFactory
    {
        public static IConfiguration OptionalAppSettingsJson =>
            new ConfigurationBuilder()
                .AddJsonFile(Environment.CurrentDirectory + "appSettings.json", optional: true)
                .Build();
    }
}
