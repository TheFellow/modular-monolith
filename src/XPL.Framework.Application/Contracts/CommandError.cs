using System;
using XPL.Modules.Kernel;

namespace XPL.Framework.Application.Contracts
{
    public class CommandError : ICorrelate
    {
        public string Error { get; protected set; }

        public DomainException? InnerException { get; }

        public Guid CorrelationId { get; set; } = Guid.Empty;

        public CommandError(string error) => Error = error;
        public CommandError(DomainException ex) : this(ex.Message) => InnerException = ex;
        private protected CommandError() => Error = "";
        public override string ToString() => Error;
    }
}
