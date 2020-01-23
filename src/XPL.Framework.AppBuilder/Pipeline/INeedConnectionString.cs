using XPL.Framework.Infrastructure.Persistence;

namespace XPL.Framework.AppBuilder.Pipeline
{
    public interface INeedConnectionString
    {
        public INeedModules WithConnectionString(ConnectionString connectionString);
    }
}
