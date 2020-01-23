using Microsoft.Extensions.Configuration;

namespace XPL.Framework.AppBuilder.Pipeline
{
    public interface INeedConfig
    {
        public INeedLogging WithConfig(IConfiguration config);
    }
}