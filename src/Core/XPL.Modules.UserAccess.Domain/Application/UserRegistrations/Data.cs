using System;
using XPL.Framework.Kernel.DateTimes;
using XPL.Framework.Kernel.Email;
using XPL.Framework.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Application.UserRegistrations;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public partial class UserRegistration
    {
        public class Data
        {
            private readonly ISystemClock _systemClock;
            private readonly Func<StatusToStringConverter> _convertFactory;

            public Data(ISystemClock systemClock, Func<StatusToStringConverter> convertFactory)
            {
                _systemClock = systemClock;
                _convertFactory = convertFactory;
            }

            public UserRegistration Raw(
                Guid registrationId,
                string email,
                string login,
                string passwordHash,
                string passwordSalt,
                string firstName,
                string lastName,
                string confirmationCode,
                string status,
                DateTime expiryDate)
            {
                var converter = _convertFactory();

                return new UserRegistration
                {
                    _email = new EmailAddress(email),
                    _login = new Login(login),
                    _password = Password.Raw(passwordHash, passwordSalt),
                    _firstName = new FirstName(firstName),
                    _lastName = new LastName(lastName),
                    _confirmationCode = confirmationCode,
                    _status = converter.ToStatus(status, expiryDate),
                    _systemClock = _systemClock,
                    RegistrationId = new RegistrationId(registrationId),
                    ExpiryDate = expiryDate
                };
            }
        }
    }
}
