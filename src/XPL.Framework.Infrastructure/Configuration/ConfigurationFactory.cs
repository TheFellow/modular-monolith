using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace XPL.Framework.Infrastructure.Configuration
{
    public static class ConfigurationFactory
    {
        public static IConfiguration OptionalAppSettingsJson =>
            new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "appSettings.json"), optional: true)
                .Build();
    }
}
