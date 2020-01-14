using System;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public class UserRegistrationError
    {
        public string Error { get; }
        public UserRegistrationError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("No error message was provided.");
            Error = error;
        }

    }
}
