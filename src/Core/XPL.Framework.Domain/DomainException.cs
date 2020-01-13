using System;

namespace XPL.Framework.Domain
{
    /// <summary>
    /// A <see cref="DomainException"/> is the only type of <see cref="Exception"/> caught by the Bus.
    /// Any data it contains should be safe to surface to the user.
    /// </summary>
    public class DomainException : Exception
    {
        public DomainException() : base() { }
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }
}
