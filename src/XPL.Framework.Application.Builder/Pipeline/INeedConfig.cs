using Microsoft.Extensions.Configuration;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedConfig
    {
        public INeedLogging WithConfig(IConfiguration config);
    }
}