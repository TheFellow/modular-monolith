using System;
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
            _login = new Login(m.Login);
            _firstName = new FirstName(m.FirstName);
            _lastName = new LastName(m.LastName);
        }

        public User(UserRegistrationConfirmed confirmed)
        {
            _userId = UserId.New();
            _registrationId = confirmed.RegistrationId;
            _login = confirmed.Login;
            _firstName = confirmed.FirstName;
            _lastName = confirmed.LastName;
        }

        public class Memento
        {
            public Guid UserId { get; }
            public Guid? RegistrationId { get; }
            public string Login { get; }
            public string FirstName { get; }
            public string LastName { get; }

            public Memento(
                Guid userId,
                Guid? registrationId,
                string login,
                string firstName,
                string lastName)
            {
                UserId = userId;
                RegistrationId = registrationId;
                Login = login;
                FirstName = firstName;
                LastName = lastName;
            }

            public User From() => new User(this);
            public static User From(Memento memento) => new User(memento);
            public static Memento Get(User u) => new Memento(
                u._userId.Value,
                u._registrationId.Id,
                u._login.Value,
                u._firstName.Value,
                u._lastName.Value);

        }
    }
}
