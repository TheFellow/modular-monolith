using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XPL.Framework.Infrastructure.Persistence
{
    public abstract class MappingRepository<TModel, TPersistence>
        where TPersistence : ISqlId
    {
        protected abstract IModelConverter<TModel, TPersistence> _converter { get; }
    }
}
