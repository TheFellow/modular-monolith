using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Framework.Modules;

namespace XPL.Modules.UserAccess.Infrastructure
{
    public class UnitOfWork : UnitOfWorkBase
    {
        public UnitOfWork(DbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
            : base(dbContext, domainEventDispatcher)
        {
        }
    }
}
