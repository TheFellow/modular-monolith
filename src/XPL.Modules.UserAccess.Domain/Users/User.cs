using Functional.Either;
using Functional.Option;
using System.Collections.Generic;
using System.Linq;
using XPL.Framework.Domain.Model;
using XPL.Modules.Kernel;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Registrations;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public partial class User : Entity
    {
        private readonly UserId _userId;
        private readonly FirstName _firstName;
        private readonly LastName _lastName;
        private readonly Login _currentLogin;
        private readonly RegistrationId _registrationId;
        private readonly IList<Role> _roles;
        
        private EmailAddress _currentEmail;
        private Password _currentPassword;

        private readonly IEmailUsage _emailUsage;

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

        public void GrantRole(Role role)
        {
            if (_roles.Contains(role))
                return;

            _roles.Add(role);
        }

        public void RevokeRole(Role role)
        {
            if (!_roles.Contains(role))
                return;

            if (role == Role.Member)
                throw new DomainException("Cannot remove the Member role");

            _roles.Remove(role);
        }
    }
}
