using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Mixology.Persistence;

internal sealed class ModuleModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context is MixologyDbContext mixology
            ? (context.GetType(), mixology.ModelConfigurationKey, designTime)
            : (object)(context.GetType(), designTime);
    }
}
