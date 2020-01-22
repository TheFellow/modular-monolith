using XPL.Framework.Domain.Model;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public partial class User : Entity
    {
        private UserId _userId;
        private RegistrationId _registrationId;
        private Login _login;
        private FirstName _firstName;
        private LastName _lastName;
    }
}
