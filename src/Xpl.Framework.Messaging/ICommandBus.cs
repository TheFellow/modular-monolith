﻿using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Framework.Messaging
{
    public interface ICommandBus
    {
        public Task<TResult> Send<TResult>(Command<TResult> command, CancellationToken cancellationToken);
    }
}