﻿using System.Threading;
using System.Threading.Tasks;

namespace XPL.Framework.Modules
{
    public interface IUnitOfWork
    {
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
    }
}
