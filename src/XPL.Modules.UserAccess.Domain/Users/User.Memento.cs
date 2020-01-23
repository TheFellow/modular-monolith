using System;
using XPL.Modules.Kernel.Email;
using XPL.Modules.Kernel.Passwords;
using XPL.Modules.UserAccess.Domain.Kernel;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public partial class User
    {
        private User(Memento m)
        {
            _userId = new UserId(m.UserId);
            _registrationId = m.RegistrationId.HasValue ? new RegistrationId(m.RegistrationId.Value) : new RegistrationId(Guid.Empty);
            _currentEmail = new EmailAddress(m.CurrentEmail);
            _currentLogin = new Login(m.CurrentLogin);
            _currentPassword = Password.Raw(m.PasswordHash, m.PasswordHash);
            _firstName = new FirstName(m.FirstName);
            _lastName = new LastName(m.LastName);
        }

        public User(UserRegistrationConfirmed confirmed)
        {
            _userId = UserId.New();
            _currentEmail = confirmed.Email;
            _currentLogin = confirmed.Login;
            _currentPassword = confirmed.Password;
            _registrationId = confirmed.RegistrationId;
            _firstName = confirmed.FirstName;
            _lastName = confirmed.LastName;
        }

        public class Memento
        {
            public Guid UserId { get; }
            public Guid? RegistrationId { get; }
            public string CurrentLogin { get; }
            public string CurrentEmail { get; }
            public string FirstName { get; }
            public string LastName { get; }
            public string PasswordHash { get; }
            public string PasswordSalt { get; }

            public Memento(
                Guid userId,
                Guid? registrationId,
                string login,
                string email,
                string firstName,
                string lastName,
                string passwordHash,
                string passwordSalt)
            {
                UserId = userId;
                RegistrationId = registrationId;
                CurrentLogin = login;
                CurrentEmail = email;
                FirstName = firstName;
                LastName = lastName;
                PasswordHash = passwordHash;
                PasswordSalt = passwordSalt;
            }

            public User From() => new User(this);
            public static User From(Memento memento) => new User(memento);
            public static Memento Get(User u) => new Memento(
                u._userId.Value,
                u._registrationId.Id,
                u._currentLogin.Value,
                u._currentEmail.Value,
                u._firstName.Value,
                u._lastName.Value,
                u._currentPassword.HashedPassword,
                u._currentPassword.Salt);

        }
    }
}
