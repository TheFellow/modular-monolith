using System;
using System.Threading;
using System.Threading.Tasks;

namespace XPL.Framework.Modules
{
    public interface IUnitOfWork : IDisposable
    {
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
    }
}
