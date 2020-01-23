using System;

namespace XPL.Framework.Kernel
{
    /// <summary>
    /// A <see cref="DomainException"/> is the only type of <see cref="Exception"/> caught by the Bus.
    /// Any data it contains should be safe to surface to the user.
    /// </summary>
    public class DomainException : Exception
    {
        public Guid CorrelationId { get; private set; } = Guid.Empty;
        public DomainException() : base() { }
        public DomainException(Guid correlationId) : base() => CorrelationId = correlationId;
        public DomainException(string message) : base(message) { }
        public DomainException(Guid correlationId, string message) : base(message) => CorrelationId = correlationId;
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
        public DomainException(Guid correlationId, string message, Exception innerException) : base(message, innerException) => CorrelationId = correlationId;
    }
}
