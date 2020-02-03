using System;
using XPL.Modules.Kernel.DateTimes;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.Registrations.Statuses;

namespace XPL.Modules.UserAccess.Domain.Registrations
{
    public partial class Registration
    {
        private Registration(Memento memento)
        {
            _systemClock = memento.SystemClock;
            RegistrationId = new RegistrationId(memento.RegistrationId);
            _email = new EmailAddress(memento.Email);
            _login = new Login(memento.Login);
            _password = Password.Raw(memento.PasswordHash, memento.PasswordSalt);
            _firstName = new FirstName(memento.FirstName);
            _lastName = new LastName(memento.LastName);
            _confirmationCode = memento.ConfirmationCode;
            ExpiryDate = memento.ExpiryDate;
            _status = memento.ToStatus();
            _statusDate = memento.StatusDate;
        }

        public sealed class Memento
        {
            public string Email { get; }
            public string Login { get; }
            public string ConfirmationCode { get; }
            public string PasswordHash { get; }
            public string PasswordSalt { get; }
            public string FirstName { get; }
            public string LastName { get; }
            public string Status { get; }
            public DateTime StatusDate { get; }
            public ISystemClock SystemClock { get; }
            public Guid RegistrationId { get; }
            public DateTime ExpiryDate { get; }

            public Memento(
                string email,
                string login,
                string confirmationCode,
                string passwordHash,
                string passwordSalt,
                string firstName,
                string lastName,
                string status,
                DateTime statusDate,
                ISystemClock systemClock,
                Guid registrationId,
                DateTime expiryDate)
            {
                Email = email;
                Login = login;
                ConfirmationCode = confirmationCode;
                PasswordHash = passwordHash;
                PasswordSalt = passwordSalt;
                FirstName = firstName;
                LastName = lastName;
                Status = status;
                StatusDate = statusDate;
                SystemClock = systemClock;
                RegistrationId = registrationId;
                ExpiryDate = expiryDate;
            }

            public Status ToStatus() => Status switch
            {
                nameof(Unconfirmed) => new Unconfirmed(SystemClock, ExpiryDate, ConfirmationCode),
                nameof(Confirmed) => new Confirmed(),
                nameof(Expired) => new Expired(),
                _ => throw new InvalidOperationException()
            };

            public Registration From() => From(this);
            public static Registration From(Memento memento) => new Registration(memento);
            public static Memento Get(Registration r) => new Memento(
                r._email.Value,
                r._login.Value,
                r._confirmationCode,
                r._password.HashedPassword,
                r._password.Salt,
                r._firstName.Value,
                r._lastName.Value,
                r._status.GetType().Name,
                r._statusDate,
                r._systemClock,
                r.RegistrationId.Id,
                r.ExpiryDate);
        }


    }
}
