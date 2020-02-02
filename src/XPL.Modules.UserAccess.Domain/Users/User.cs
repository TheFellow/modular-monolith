using Functional.Either;
using Functional.Option;
using XPL.Framework.Domain.Model;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Registrations;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public partial class User : Entity
    {
        private UserId _userId;
        private EmailAddress _currentEmail;
        private FirstName _firstName;
        private LastName _lastName;
        private Login _currentLogin;
        private Password _currentPassword;
        private RegistrationId _registrationId;

        private IEmailUsage _emailUsage;

        public Either<UserAccessError, PasswordUpdated> UpdatePassword(string oldPassword, string newPassword)
        {
            if (!_currentPassword.Verify(oldPassword))
                return new UserAccessError("Incorrect password");

            if (oldPassword == newPassword)
                return new UserAccessError("Cannot change to the same password");

            _currentPassword = new Password(newPassword);
            return new PasswordUpdated();
        }

        public Option<UserAccessError> UpdateEmail(EmailAddress newEmail)
        {
            if (_currentEmail == newEmail)
                return new UserAccessError("Cannot change to the same address");

            Option<Login> emailUsage = _emailUsage.TryGetLoginForEmail(newEmail);

            if (emailUsage is Some<Login> login && login != _currentLogin)
                return new UserAccessError("This login name is already in use");

            _currentEmail = newEmail;
            return None.Value;
        }
    }
}
