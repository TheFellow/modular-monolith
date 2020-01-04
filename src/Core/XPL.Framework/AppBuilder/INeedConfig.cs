using Microsoft.Extensions.Configuration;

namespace XPL.Framework.AppBuilder
{
    public interface INeedConfig
    {
        public INeedLogging WithConfig(IConfiguration config);
    }
}