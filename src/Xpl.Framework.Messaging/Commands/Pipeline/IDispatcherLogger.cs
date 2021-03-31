using System;

namespace Xpl.Framework.Messaging.Commands.Pipeline
{
    public interface IDispatcherLogger
    {
        void Sending(Type commandType, Guid correlationId);
        void Success(Guid correlationId);
        void Error(Guid correlationId, string message);
    }
}
