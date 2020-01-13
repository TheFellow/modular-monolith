using XPL.Framework.Kernel.Email;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration
    {
        private EmailAddress _email;
        private Login _login;
        private Password _password;
        private FirstName _firstName;
        private LastName _lastName;

        public RegistrationId RegistrationId { get; private set; }

        #region Private parameterless ctor
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        private UserRegistration() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable. 
        #endregion
    }
}
