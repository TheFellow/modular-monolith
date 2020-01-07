using Microsoft.Extensions.Configuration;

namespace XPL.Framework.Application.Builder
{
    public interface INeedConfig
    {
        public INeedLogging WithConfig(IConfiguration config);
    }
}