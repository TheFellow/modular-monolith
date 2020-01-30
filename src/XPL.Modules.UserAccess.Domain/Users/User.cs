using Functional.Either;
using Functional.Option;
using XPL.Framework.Domain.Model;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

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

        public Either<UserError, PasswordUpdated> UpdatePassword(string oldPassword, string newPassword)
        {
            if (!_currentPassword.Verify(oldPassword))
                return new UserError("Incorrect password");

            if (oldPassword == newPassword)
                return new UserError("Cannot change to the same password");

            _currentPassword = new Password(newPassword);
            return new PasswordUpdated();
        }

        public Option<UserError> UpdateEmail(EmailAddress newEmail)
        {
            if (_currentEmail == newEmail)
                return new UserError("Cannot change to the same address");

            Option<Login> emailUsage = _emailUsage.TryGetLoginForEmail(newEmail);

            if (emailUsage is Some<Login> login && login != _currentLogin)
                return new UserError("This login name is already in use");

            _currentEmail = newEmail;
            return None.Value;
        }
    }
}
