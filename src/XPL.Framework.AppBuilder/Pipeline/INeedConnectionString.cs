using Microsoft.Extensions.Configuration;
using System;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Framework.AppBuilder.Pipeline
{
    public interface INeedConnectionString
    {
        public INeedModules WithConnectionString(Func<IConfiguration, ConnectionString> connectionString);
    }
}
