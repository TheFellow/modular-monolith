using System;
using System.Threading;
using System.Threading.Tasks;

namespace XPL.Framework.Application.Ports
{
    public interface IUnitOfWork : IDisposable
    {
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
    }
}
