using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Framework.Application.Builder.Pipeline
{
    public interface INeedConnectionString
    {
        public INeedBus WithConnectionString(ConnectionString connectionString);
    }
}
